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

    [Theory]
    [InlineData(CdrEndianness.LittleEndian)]
    [InlineData(CdrEndianness.BigEndian)]
    public void 全フィールドの_PL_CDR_往復_Writer(CdrEndianness endian)
    {
        var src = MakeWriter();
        var buf = new byte[1024];
        var w = new CdrWriter(buf, endian);
        DiscoveredEndpointDataSerializer.Write(ref w, src);
        int written = w.Position;

        var r = new CdrReader(buf.AsSpan(0, written), endian);
        var read = DiscoveredEndpointDataSerializer.Read(ref r, EndpointKind.Writer);

        read.Kind.Should().Be(EndpointKind.Writer);
        read.ParticipantGuid.Should().Be(src.ParticipantGuid);
        read.EndpointGuid.Should().Be(src.EndpointGuid);
        read.TopicName.Should().Be(src.TopicName);
        read.TypeName.Should().Be(src.TypeName);
        read.Reliability.Should().Be(src.Reliability);
        read.Durability.Should().Be(src.Durability);
        read.Deadline.Should().Be(src.Deadline);
        read.LatencyBudget.Should().Be(src.LatencyBudget);
        read.Liveliness.Should().Be(src.Liveliness);
        read.Ownership.Should().Be(src.Ownership);
        read.DestinationOrder.Should().Be(src.DestinationOrder);
        read.Presentation.Should().Be(src.Presentation);
        read.Partition.Should().Be(src.Partition);
    }

    [Theory]
    [InlineData(CdrEndianness.LittleEndian)]
    [InlineData(CdrEndianness.BigEndian)]
    public void Reliable_durability_TransientLocal_の往復(CdrEndianness endian)
    {
        var src = MakeWriter();
        src.Reliability = new ReliabilityQos(ReliabilityKind.Reliable,
            Duration.FromTimeSpan(TimeSpan.FromMilliseconds(500)));
        src.Durability = DurabilityQos.TransientLocal;

        var buf = new byte[1024];
        var w = new CdrWriter(buf, endian);
        DiscoveredEndpointDataSerializer.Write(ref w, src);

        var r = new CdrReader(buf.AsSpan(0, w.Position), endian);
        var read = DiscoveredEndpointDataSerializer.Read(ref r, EndpointKind.Writer);

        read.Reliability.Kind.Should().Be(ReliabilityKind.Reliable);
        read.Reliability.MaxBlockingTime.ToTimeSpan().TotalMilliseconds.Should().BeApproximately(500, 1);
        read.Durability.Kind.Should().Be(DurabilityKind.TransientLocal);
    }

    [Theory]
    [InlineData(CdrEndianness.LittleEndian)]
    [InlineData(CdrEndianness.BigEndian)]
    public void 非デフォルト_QoS_の往復(CdrEndianness endian)
    {
        var src = MakeWriter();
        src.Deadline = new DeadlineQos(Duration.FromTimeSpan(TimeSpan.FromSeconds(5)));
        src.LatencyBudget = new LatencyBudgetQos(Duration.FromTimeSpan(TimeSpan.FromMilliseconds(50)));
        src.Liveliness = new LivelinessQos(LivelinessKind.ManualByTopic, Duration.FromTimeSpan(TimeSpan.FromSeconds(10)));
        src.Ownership = new OwnershipQos(OwnershipKind.Exclusive);
        src.DestinationOrder = new DestinationOrderQos(DestinationOrderKind.BySourceTimestamp);
        src.Presentation = new PresentationQos(PresentationAccessScope.Topic, true, true);
        src.Partition = new PartitionQos("partition_a", "partition_b");

        var buf = new byte[2048];
        var w = new CdrWriter(buf, endian);
        DiscoveredEndpointDataSerializer.Write(ref w, src);

        var r = new CdrReader(buf.AsSpan(0, w.Position), endian);
        var read = DiscoveredEndpointDataSerializer.Read(ref r, EndpointKind.Writer);

        read.Deadline.Period.ToTimeSpan().TotalSeconds.Should().BeApproximately(5, 0.01);
        read.LatencyBudget.Duration.ToTimeSpan().TotalMilliseconds.Should().BeApproximately(50, 1);
        read.Liveliness.Kind.Should().Be(LivelinessKind.ManualByTopic);
        read.Liveliness.LeaseDuration.ToTimeSpan().TotalSeconds.Should().BeApproximately(10, 0.01);
        read.Ownership.Kind.Should().Be(OwnershipKind.Exclusive);
        read.DestinationOrder.Kind.Should().Be(DestinationOrderKind.BySourceTimestamp);
        read.Presentation.AccessScope.Should().Be(PresentationAccessScope.Topic);
        read.Presentation.CoherentAccess.Should().BeTrue();
        read.Presentation.OrderedAccess.Should().BeTrue();
        read.Partition.Names.Count.Should().Be(2);
        read.Partition.Names[0].Should().Be("partition_a");
        read.Partition.Names[1].Should().Be("partition_b");
    }

    [Theory]
    [InlineData(CdrEndianness.LittleEndian, (byte)0x03)] // PL_CDR_LE
    [InlineData(CdrEndianness.BigEndian, (byte)0x02)]     // PL_CDR_BE
    public void encap_PL_CDR_を含む_serializedPayload_全体(CdrEndianness endian, byte expectedEncapKind)
    {
        var src = MakeWriter();
        var buf = new byte[1024];
        var encapKind = CdrEncapsulation.ParameterListCdr(endian);
        CdrEncapsulation.Write(buf, encapKind);
        var w = new CdrWriter(buf, endian, cdrOrigin: CdrEncapsulation.Size);
        DiscoveredEndpointDataSerializer.Write(ref w, src);
        int total = w.Position;

        // 先頭 4B encap
        buf[0].Should().Be((byte)0x00);
        buf[1].Should().Be(expectedEncapKind);

        // Read back
        var (kind, _) = CdrEncapsulation.Read(buf.AsSpan(0, 4));
        var readEndian = CdrEncapsulation.GetEndianness(kind);
        readEndian.Should().Be(endian);
        var r = new CdrReader(buf.AsSpan(0, total), readEndian, cdrOrigin: CdrEncapsulation.Size);
        var read = DiscoveredEndpointDataSerializer.Read(ref r, EndpointKind.Writer);
        read.EndpointGuid.Should().Be(src.EndpointGuid);
        read.TopicName.Should().Be(src.TopicName);
    }
}
