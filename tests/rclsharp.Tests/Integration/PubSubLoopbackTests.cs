using System.Net;
using Rclsharp.Common;
using Rclsharp.Dds;
using Rclsharp.Msgs.Std;
using Rclsharp.Transport;

namespace Rclsharp.Tests.Integration;

public class PubSubLoopbackTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(2);

    private sealed class TestEnv
    {
        public required LoopbackHub Hub { get; init; }
        public required DomainParticipant ParticipantA { get; init; }
        public required DomainParticipant ParticipantB { get; init; }
    }

    private static TestEnv CreatePair()
    {
        var hub = new LoopbackHub();
        var multicastIp = IPAddress.Parse("239.255.0.1");
        var spdpLoc = Locator.FromUdpV4(multicastIp, 7400u);
        var userMcLoc = Locator.FromUdpV4(multicastIp, 7401u);

        var spdpA = hub.Create(spdpLoc);
        var spdpB = hub.Create(spdpLoc);
        var ucA = hub.Create(Locator.FromUdpV4(IPAddress.Parse("10.0.0.1"), 7411u));
        var ucB = hub.Create(Locator.FromUdpV4(IPAddress.Parse("10.0.0.2"), 7413u));
        var userMcA = hub.Create(userMcLoc);
        var userMcB = hub.Create(userMcLoc);
        var userUcA = hub.Create(Locator.FromUdpV4(IPAddress.Parse("10.0.0.1"), 7412u));
        var userUcB = hub.Create(Locator.FromUdpV4(IPAddress.Parse("10.0.0.2"), 7414u));

        var optionsA = new DomainParticipantOptions
        {
            DomainId = 0, ParticipantId = 1, EntityName = "node_a",
            MulticastGroup = multicastIp,
            SpdpInterval = TimeSpan.FromMilliseconds(50),
            CustomMulticastTransport = spdpA,
            CustomUnicastTransport = ucA,
            CustomUserMulticastTransport = userMcA,
            CustomUserUnicastTransport = userUcA,
        };
        var optionsB = new DomainParticipantOptions
        {
            DomainId = 0, ParticipantId = 2, EntityName = "node_b",
            MulticastGroup = multicastIp,
            SpdpInterval = TimeSpan.FromMilliseconds(50),
            CustomMulticastTransport = spdpB,
            CustomUnicastTransport = ucB,
            CustomUserMulticastTransport = userMcB,
            CustomUserUnicastTransport = userUcB,
        };

        var pA = new DomainParticipant(optionsA);
        var pB = new DomainParticipant(optionsB);
        return new TestEnv { Hub = hub, ParticipantA = pA, ParticipantB = pB };
    }

    [Fact]
    public async Task chatter_を_Publisher_から_Subscription_に届ける()
    {
        var env = CreatePair();
        using var pA = env.ParticipantA;
        using var pB = env.ParticipantB;

        var receivedTcs = new TaskCompletionSource<StringMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sub = pB.CreateSubscription<StringMessage>(
            "chatter",
            StringMessageSerializer.Instance,
            (msg, _) => receivedTcs.TrySetResult(msg));

        using var pub = pA.CreatePublisher<StringMessage>("chatter", StringMessageSerializer.Instance);

        pA.Start();
        pB.Start();

        await pub.PublishAsync(new StringMessage("hello rclsharp"));

        var received = await receivedTcs.Task.WaitAsync(ReceiveTimeout);
        received.Data.Should().Be("hello rclsharp");
    }

    [Fact]
    public async Task 異なる_topic_は_互いに届かない()
    {
        var env = CreatePair();
        using var pA = env.ParticipantA;
        using var pB = env.ParticipantB;

        bool received = false;
        using var sub = pB.CreateSubscription<StringMessage>(
            "topic_a",
            StringMessageSerializer.Instance,
            (_, _) => received = true);

        using var pub = pA.CreatePublisher<StringMessage>("topic_b", StringMessageSerializer.Instance);

        pA.Start();
        pB.Start();

        await pub.PublishAsync(new StringMessage("should not arrive"));
        await Task.Delay(200); // 受信猶予

        received.Should().BeFalse();
    }

    [Fact]
    public async Task 複数件_publish_すると_順番に_受信()
    {
        var env = CreatePair();
        using var pA = env.ParticipantA;
        using var pB = env.ParticipantB;

        var received = new List<string>();
        var lockObj = new object();
        using var sub = pB.CreateSubscription<StringMessage>(
            "chatter",
            StringMessageSerializer.Instance,
            (msg, _) =>
            {
                lock (lockObj) { received.Add(msg.Data); }
            });

        using var pub = pA.CreatePublisher<StringMessage>("chatter", StringMessageSerializer.Instance);

        pA.Start();
        pB.Start();

        for (int i = 0; i < 10; i++)
        {
            await pub.PublishAsync(new StringMessage($"msg{i}"));
        }

        // 受信猶予 (Loopback は同期配信なので即時のはず)
        await Task.Delay(100);

        lock (lockObj)
        {
            received.Should().HaveCount(10);
            received.Should().Equal(Enumerable.Range(0, 10).Select(i => $"msg{i}"));
        }
    }

    [Fact]
    public async Task 同一_participant_内で_自分の_publish_を_subscription_が_受信できる()
    {
        var env = CreatePair();
        using var pA = env.ParticipantA;
        using var pB = env.ParticipantB;

        var receivedTcs = new TaskCompletionSource<StringMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        // pA で pub と sub の両方
        using var sub = pA.CreateSubscription<StringMessage>(
            "chatter",
            StringMessageSerializer.Instance,
            (msg, _) => receivedTcs.TrySetResult(msg));
        using var pub = pA.CreatePublisher<StringMessage>("chatter", StringMessageSerializer.Instance);

        pA.Start();
        pB.Start();

        await pub.PublishAsync(new StringMessage("self-pub"));
        var received = await receivedTcs.Task.WaitAsync(ReceiveTimeout);
        received.Data.Should().Be("self-pub");
    }
}
