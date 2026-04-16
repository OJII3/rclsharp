using Rclsharp.Cdr;
using Rclsharp.Common;
using Rclsharp.Dds.QoS;
using Rclsharp.Discovery;

using Guid = Rclsharp.Common.Guid;

namespace Rclsharp.Tests.Discovery;

public class DiscoveredEndpointDataSerializerTests
{
    private static DiscoveredEndpointData MakeWriter()
    {
        var participantPrefix = GuidPrefix.Create(VendorId.Rclsharp, 0x11223344, 0x55667788, 0x9999);
        var writerEntityId = new EntityId(0x0000_1234, EntityKind.UserDefinedWriterNoKey);
        return new DiscoveredEndpointData
        {
            Kind = EndpointKind.Writer,
            ParticipantGuid = new Guid(participantPrefix, BuiltinEntityIds.Participant),
            EndpointGuid = new Guid(participantPrefix, writerEntityId),
            TopicName = "rt/chatter",
            TypeName = "std_msgs::msg::dds_::String_",
            Reliability = ReliabilityQos.BestEffort,
            Durability = DurabilityQos.Volatile,
        };
    }

    [Fact]
    public void 全フィールドの_PL_CDR_往復_Writer()
    {
        var src = MakeWriter();
        var buf = new byte[1024];
        var w = new CdrWriter(buf, CdrEndianness.LittleEndian);
        DiscoveredEndpointDataSerializer.Write(ref w, src);
        int written = w.Position;

        var r = new CdrReader(buf.AsSpan(0, written), CdrEndianness.LittleEndian);
        var read = DiscoveredEndpointDataSerializer.Read(ref r, EndpointKind.Writer);

        read.Kind.Should().Be(EndpointKind.Writer);
        read.ParticipantGuid.Should().Be(src.ParticipantGuid);
        read.EndpointGuid.Should().Be(src.EndpointGuid);
        read.TopicName.Should().Be(src.TopicName);
        read.TypeName.Should().Be(src.TypeName);
        read.Reliability.Should().Be(src.Reliability);
        read.Durability.Should().Be(src.Durability);
    }

    [Fact]
    public void Reliable_durability_TransientLocal_の往復()
    {
        var src = MakeWriter();
        src.Reliability = new ReliabilityQos(ReliabilityKind.Reliable,
            Duration.FromTimeSpan(TimeSpan.FromMilliseconds(500)));
        src.Durability = DurabilityQos.TransientLocal;

        var buf = new byte[1024];
        var w = new CdrWriter(buf, CdrEndianness.LittleEndian);
        DiscoveredEndpointDataSerializer.Write(ref w, src);

        var r = new CdrReader(buf.AsSpan(0, w.Position), CdrEndianness.LittleEndian);
        var read = DiscoveredEndpointDataSerializer.Read(ref r, EndpointKind.Writer);

        read.Reliability.Kind.Should().Be(ReliabilityKind.Reliable);
        read.Reliability.MaxBlockingTime.ToTimeSpan().TotalMilliseconds.Should().BeApproximately(500, 1);
        read.Durability.Kind.Should().Be(DurabilityKind.TransientLocal);
    }

    [Fact]
    public void encap_PL_CDR_LE_を含む_serializedPayload_全体()
    {
        var src = MakeWriter();
        var buf = new byte[1024];
        CdrEncapsulation.Write(buf, CdrEncapsulation.PlCdrLittleEndian);
        var w = new CdrWriter(buf, CdrEndianness.LittleEndian, cdrOrigin: CdrEncapsulation.Size);
        DiscoveredEndpointDataSerializer.Write(ref w, src);
        int total = w.Position;

        // 先頭 4B encap
        buf[0].Should().Be((byte)0x00);
        buf[1].Should().Be((byte)0x03); // PL_CDR_LE

        // Read back
        var (kind, _) = CdrEncapsulation.Read(buf.AsSpan(0, 4));
        var endian = CdrEncapsulation.GetEndianness(kind);
        var r = new CdrReader(buf.AsSpan(0, total), endian, cdrOrigin: CdrEncapsulation.Size);
        var read = DiscoveredEndpointDataSerializer.Read(ref r, EndpointKind.Writer);
        read.EndpointGuid.Should().Be(src.EndpointGuid);
        read.TopicName.Should().Be(src.TopicName);
    }
}
