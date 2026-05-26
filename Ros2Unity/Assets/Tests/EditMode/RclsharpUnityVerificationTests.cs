using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;
using Rclsharp.Common;
using Rclsharp.Dds;
using Rclsharp.Msgs.Std;
using Rclsharp.Transport;
using Unity.PerformanceTesting;
using UnityEngine.Profiling;

namespace Rclsharp.UnityVerification.Tests
{
    public sealed class RclsharpUnityVerificationTests
    {
        private const string SmokeTopic = "unity_chatter";
        private const string PerfTopic = "unity_perf_chatter";
        private const int SmokeMessageCount = 16;
        private const int WarmupMessageCount = 32;
        private const int PerformanceMessageCount = 1000;
        private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);

        [Test]
        public void Loopback_pubsub_で_StringMessage_を順序通り受信できる()
        {
            using var pair = CreateLoopbackPair();
            var received = new List<string>(SmokeMessageCount);
            var gate = new object();

            using var sub = pair.Reader.CreateSubscription<StringMessage>(
                SmokeTopic,
                StringMessageSerializer.Instance,
                (msg, _) =>
                {
                    lock (gate)
                    {
                        received.Add(msg.Data);
                    }
                });

            using var pub = pair.Writer.CreatePublisher<StringMessage>(
                SmokeTopic,
                StringMessageSerializer.Instance);

            pair.Start();
            AssertDiscovered(pair.Writer, pair.Reader, "rt/" + SmokeTopic);

            for (int i = 0; i < SmokeMessageCount; i++)
            {
                pub.PublishAsync(new StringMessage("unity-smoke-" + i)).GetAwaiter().GetResult();
            }

            Assert.IsTrue(WaitUntil(() =>
            {
                lock (gate)
                {
                    return received.Count == SmokeMessageCount;
                }
            }, ReceiveTimeout), "Unity loopback smoke test timed out before all messages arrived.");

            lock (gate)
            {
                CollectionAssert.AreEqual(
                    Enumerable.Range(0, SmokeMessageCount).Select(i => "unity-smoke-" + i).ToArray(),
                    received);
            }
        }

        [Test]
        [Performance]
        public void Loopback_pubsub_の通信時間とメモリ差分を記録する()
        {
            using var pair = CreateLoopbackPair();
            int received = 0;

            using var sub = pair.Reader.CreateSubscription<StringMessage>(
                PerfTopic,
                StringMessageSerializer.Instance,
                (_, _) => Interlocked.Increment(ref received));

            using var pub = pair.Writer.CreatePublisher<StringMessage>(
                PerfTopic,
                StringMessageSerializer.Instance);

            pair.Start();
            AssertDiscovered(pair.Writer, pair.Reader, "rt/" + PerfTopic);

            var warmupMessages = CreateMessages("unity-warmup", WarmupMessageCount);
            for (int i = 0; i < warmupMessages.Length; i++)
            {
                pub.PublishAsync(warmupMessages[i]).GetAwaiter().GetResult();
            }
            Assert.IsTrue(
                WaitUntil(() => Volatile.Read(ref received) >= WarmupMessageCount, ReceiveTimeout),
                "Warmup messages were not received before performance measurement.");

            var messages = CreateMessages("unity-perf", PerformanceMessageCount);
            ForceFullCollection();

            Interlocked.Exchange(ref received, 0);
            long managedBefore = GC.GetTotalMemory(forceFullCollection: true);
            long currentThreadAllocatedBefore = TryGetCurrentThreadAllocatedBytes();
            long unityTotalAllocatedBefore = Profiler.GetTotalAllocatedMemoryLong();
            long unityMonoUsedBefore = Profiler.GetMonoUsedSizeLong();

            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < messages.Length; i++)
            {
                pub.PublishAsync(messages[i]).GetAwaiter().GetResult();
            }
            Assert.IsTrue(
                WaitUntil(() => Volatile.Read(ref received) >= PerformanceMessageCount, ReceiveTimeout),
                "Performance messages were not received before timeout.");
            stopwatch.Stop();

            long currentThreadAllocatedAfter = TryGetCurrentThreadAllocatedBytes();
            long unityTotalAllocatedAfter = Profiler.GetTotalAllocatedMemoryLong();
            long unityMonoUsedAfter = Profiler.GetMonoUsedSizeLong();
            long managedAfter = GC.GetTotalMemory(forceFullCollection: true);

            double elapsedMilliseconds = Math.Max(stopwatch.Elapsed.TotalMilliseconds, 0.001d);
            double messagesPerSecond = PerformanceMessageCount / stopwatch.Elapsed.TotalSeconds;

            Measure.Custom(new SampleGroup("rclsharp.pubsub.elapsed_ms", SampleUnit.Millisecond, false), elapsedMilliseconds);
            Measure.Custom(new SampleGroup("rclsharp.pubsub.mean_message_ms", SampleUnit.Millisecond, false), elapsedMilliseconds / PerformanceMessageCount);
            Measure.Custom(new SampleGroup("rclsharp.pubsub.messages_per_second", SampleUnit.Undefined, true), messagesPerSecond);
            Measure.Custom(new SampleGroup("rclsharp.memory.managed_heap_delta_bytes", SampleUnit.Byte, false), PositiveDelta(managedAfter, managedBefore));
            Measure.Custom(new SampleGroup("rclsharp.memory.unity_total_allocated_delta_bytes", SampleUnit.Byte, false), PositiveDelta(unityTotalAllocatedAfter, unityTotalAllocatedBefore));
            Measure.Custom(new SampleGroup("rclsharp.memory.unity_mono_used_delta_bytes", SampleUnit.Byte, false), PositiveDelta(unityMonoUsedAfter, unityMonoUsedBefore));
            RecordAvailableDelta(
                "rclsharp.memory.current_thread_allocated_delta_bytes",
                currentThreadAllocatedBefore,
                currentThreadAllocatedAfter);

            Assert.AreEqual(PerformanceMessageCount, Volatile.Read(ref received));
        }

        private static LoopbackParticipantPair CreateLoopbackPair()
        {
            var hub = new LoopbackHub();
            var multicastIp = IPAddress.Parse("239.255.0.1");
            var spdpLocator = Locator.FromUdpV4(multicastIp, 7400u);
            var userMulticastLocator = Locator.FromUdpV4(multicastIp, 7401u);

            var writer = new DomainParticipant(new DomainParticipantOptions
            {
                DomainId = 0,
                ParticipantId = 41,
                EntityName = "rclsharp_unity_writer",
                MulticastGroup = multicastIp,
                SpdpInterval = TimeSpan.FromMilliseconds(25),
                SedpInterval = TimeSpan.FromMilliseconds(25),
                CustomMulticastTransport = hub.Create(spdpLocator),
                CustomUnicastTransport = hub.Create(Locator.FromUdpV4(IPAddress.Parse("10.42.0.1"), 7482u)),
                CustomUserMulticastTransport = hub.Create(userMulticastLocator),
                CustomUserUnicastTransport = hub.Create(Locator.FromUdpV4(IPAddress.Parse("10.42.0.1"), 7483u)),
            });

            var reader = new DomainParticipant(new DomainParticipantOptions
            {
                DomainId = 0,
                ParticipantId = 42,
                EntityName = "rclsharp_unity_reader",
                MulticastGroup = multicastIp,
                SpdpInterval = TimeSpan.FromMilliseconds(25),
                SedpInterval = TimeSpan.FromMilliseconds(25),
                CustomMulticastTransport = hub.Create(spdpLocator),
                CustomUnicastTransport = hub.Create(Locator.FromUdpV4(IPAddress.Parse("10.42.0.2"), 7484u)),
                CustomUserMulticastTransport = hub.Create(userMulticastLocator),
                CustomUserUnicastTransport = hub.Create(Locator.FromUdpV4(IPAddress.Parse("10.42.0.2"), 7485u)),
            });

            return new LoopbackParticipantPair(writer, reader);
        }

        private static void AssertDiscovered(
            DomainParticipant writerParticipant,
            DomainParticipant readerParticipant,
            string ddsTopic)
        {
            bool discovered = WaitUntil(
                () => ReaderSawWriter(readerParticipant, writerParticipant, ddsTopic)
                   && WriterSawReader(writerParticipant, readerParticipant, ddsTopic),
                ReceiveTimeout);

            Assert.IsTrue(discovered, "SEDP discovery did not complete for topic " + ddsTopic + ".");
        }

        private static bool ReaderSawWriter(
            DomainParticipant readerParticipant,
            DomainParticipant writerParticipant,
            string ddsTopic)
        {
            return readerParticipant.DiscoveryDb.WriterSnapshot()
                .Any(ep => ep.Data.TopicName == ddsTopic
                        && ep.Data.ParticipantGuid.Prefix.Equals(writerParticipant.GuidPrefix));
        }

        private static bool WriterSawReader(
            DomainParticipant writerParticipant,
            DomainParticipant readerParticipant,
            string ddsTopic)
        {
            return writerParticipant.DiscoveryDb.ReaderSnapshot()
                .Any(ep => ep.Data.TopicName == ddsTopic
                        && ep.Data.ParticipantGuid.Prefix.Equals(readerParticipant.GuidPrefix));
        }

        private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < timeout)
            {
                if (condition())
                {
                    return true;
                }
                Thread.Sleep(10);
            }
            return condition();
        }

        private static StringMessage[] CreateMessages(string prefix, int count)
        {
            var messages = new StringMessage[count];
            for (int i = 0; i < messages.Length; i++)
            {
                messages[i] = new StringMessage(prefix + "-" + i);
            }
            return messages;
        }

        private static void ForceFullCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static long PositiveDelta(long after, long before)
            => Math.Max(0L, after - before);

        private static long TryGetCurrentThreadAllocatedBytes()
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            if (method is null)
            {
                return -1L;
            }

            return (long)method.Invoke(null, null);
        }

        private static void RecordAvailableDelta(string name, long before, long after)
        {
            if (before < 0L || after < 0L)
            {
                return;
            }

            Measure.Custom(new SampleGroup(name, SampleUnit.Byte, false), PositiveDelta(after, before));
        }

        private sealed class LoopbackParticipantPair : IDisposable
        {
            public LoopbackParticipantPair(DomainParticipant writer, DomainParticipant reader)
            {
                Writer = writer;
                Reader = reader;
            }

            public DomainParticipant Writer { get; }
            public DomainParticipant Reader { get; }

            public void Start()
            {
                Writer.Start();
                Reader.Start();
            }

            public void Dispose()
            {
                Writer.Dispose();
                Reader.Dispose();
            }
        }
    }
}
