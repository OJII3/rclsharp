using System.Net;
using Rclsharp.Common;
using Rclsharp.Dds;
using Rclsharp.Discovery;
using Rclsharp.Msgs.Std;
using Rclsharp.Transport;

using Guid = Rclsharp.Common.Guid;

namespace Rclsharp.Tests.Integration;

public class SedpReliableLoopbackTests
{
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(3);

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
        var ucALoc = Locator.FromUdpV4(IPAddress.Parse("10.0.0.1"), 7411u);
        var ucBLoc = Locator.FromUdpV4(IPAddress.Parse("10.0.0.2"), 7413u);

        var spdpA = hub.Create(spdpLoc);
        var spdpB = hub.Create(spdpLoc);
        var ucA = hub.Create(ucALoc);
        var ucB = hub.Create(ucBLoc);
        var userMcA = hub.Create(userMcLoc);
        var userMcB = hub.Create(userMcLoc);
        var userUcA = hub.Create(Locator.FromUdpV4(IPAddress.Parse("10.0.0.1"), 7412u));
        var userUcB = hub.Create(Locator.FromUdpV4(IPAddress.Parse("10.0.0.2"), 7414u));

        var optionsA = new DomainParticipantOptions
        {
            DomainId = 0, ParticipantId = 1, EntityName = "node_a",
            MulticastGroup = multicastIp,
            SpdpInterval = TimeSpan.FromMilliseconds(50),
            SedpInterval = TimeSpan.FromMilliseconds(50),
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
            SedpInterval = TimeSpan.FromMilliseconds(50),
            CustomMulticastTransport = spdpB,
            CustomUnicastTransport = ucB,
            CustomUserMulticastTransport = userMcB,
            CustomUserUnicastTransport = userUcB,
        };

        return new TestEnv
        {
            Hub = hub,
            ParticipantA = new DomainParticipant(optionsA),
            ParticipantB = new DomainParticipant(optionsB),
        };
    }

    [Fact]
    public async Task SPDP_発見後に_SEDP_endpoint_が_auto_match_される()
    {
        var env = CreatePair();
        using var pA = env.ParticipantA;
        using var pB = env.ParticipantB;

        pA.Start();
        pB.Start();

        // SPDP 周期 (50ms) を 4 サイクル待つ
        await Task.Delay(300);

        // pB の SedpPublicationsWriter は pA の SedpPublicationsReader を match しているはず
        // (matched count > 0 を間接的に確認するため、pA の Pub を作って pB が見えるか試す)
        using var pubA = pA.CreatePublisher<StringMessage>(
            "chatter", StringMessageSerializer.Instance, typeName: StringMessage.DdsTypeName);

        // SEDP Reliable 経由で endpoint が pB に届くまで待つ
        var deadline = DateTime.UtcNow + DiscoveryTimeout;
        bool found = false;
        while (DateTime.UtcNow < deadline)
        {
            var snapshot = pB.DiscoveryDb.WriterSnapshot();
            if (snapshot.Any(ep => ep.Data.TopicName == "rt/chatter"
                                && ep.Data.ParticipantGuid.Prefix.Equals(pA.GuidPrefix)))
            {
                found = true;
                break;
            }
            await Task.Delay(50);
        }
        found.Should().BeTrue("pA の Publisher endpoint が SEDP 経由で pB に届くべき");
    }

    [Fact]
    public async Task SEDP_writer_の_HighestAcked_が_reliable_handshake_で進む()
    {
        var env = CreatePair();
        using var pA = env.ParticipantA;
        using var pB = env.ParticipantB;

        pA.Start();
        pB.Start();

        using var pubA = pA.CreatePublisher<StringMessage>(
            "chatter", StringMessageSerializer.Instance, typeName: StringMessage.DdsTypeName);

        // SPDP→SEDP auto-match→DATA→HB→ACKNACK の周回を待つ
        var pubsWriter = GetSedpPublicationsWriter(pA);
        pubsWriter.Should().NotBeNull();

        var deadline = DateTime.UtcNow + DiscoveryTimeout;
        bool acked = false;
        while (DateTime.UtcNow < deadline)
        {
            // pB の SEDP reader (Pub) が pA の SEDP writer (Pub) に対して送り返した ACKNACK で
            // pubsWriter のいずれかの ReaderProxy.HighestAcked が >= 1 になるはず
            foreach (var proxy in pubsWriter!.Stateful.MatchedReaders)
            {
                if (proxy.HighestAcked.Value >= 1L)
                {
                    acked = true;
                    break;
                }
            }
            if (acked) break;
            await Task.Delay(50);
        }
        acked.Should().BeTrue("SEDP writer が ACKNACK を受けて HighestAcked が進むべき");
    }

    [Fact]
    public async Task 後発の_Pub_作成も_TRANSIENT_LOCAL_風に_既存ノードへ届く()
    {
        var env = CreatePair();
        using var pA = env.ParticipantA;
        using var pB = env.ParticipantB;

        pA.Start();
        pB.Start();
        await Task.Delay(200); // SPDP 安定化

        // pB が起動した後で pA が Pub を作る
        using var pubA = pA.CreatePublisher<StringMessage>(
            "late_topic", StringMessageSerializer.Instance, typeName: StringMessage.DdsTypeName);

        var deadline = DateTime.UtcNow + DiscoveryTimeout;
        bool found = false;
        while (DateTime.UtcNow < deadline)
        {
            if (pB.DiscoveryDb.WriterSnapshot().Any(ep => ep.Data.TopicName == "rt/late_topic"))
            {
                found = true;
                break;
            }
            await Task.Delay(50);
        }
        found.Should().BeTrue();
    }

    private static SedpEndpointWriter? GetSedpPublicationsWriter(DomainParticipant p)
    {
        // private field にアクセスするため reflection
        var field = typeof(DomainParticipant).GetField("_sedpPublicationsWriter",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field?.GetValue(p) as SedpEndpointWriter;
    }
}
