using System.Net;
using Rclsharp.Cdr;
using Rclsharp.Common;
using Rclsharp.Discovery;

using Guid = Rclsharp.Common.Guid;

namespace Rclsharp.Tests.Discovery;

public class ParticipantDataSerializerTests
{
    private static ParticipantData MakeSampleData()
    {
        var prefix = GuidPrefix.Create(VendorId.Rclsharp, 0x11223344, 0x55667788, 0x99AA);
        var data = new ParticipantData
        {
            ProtocolVersion = ProtocolVersion.V2_4,
            VendorId = VendorId.Rclsharp,
            Guid = new Guid(prefix, BuiltinEntityIds.Participant),
            BuiltinEndpoints = BuiltinEndpointSet.RclsharpDefault,
            LeaseDuration = Duration.FromSeconds(20),
            ExpectsInlineQos = false,
            EntityName = "rclsharp_test_node",
        };
        data.MetatrafficUnicastLocators.Add(Locator.FromUdpV4(IPAddress.Parse("192.168.1.10"), 7411u));
        data.MetatrafficMulticastLocators.Add(Locator.FromUdpV4(IPAddress.Parse("239.255.0.1"), 7400u));
        return data;
    }

    [Fact]
    public void 全フィールドの_PL_CDR_往復()
    {
        var src = MakeSampleData();
        var buf = new byte[1024];
        var w = new CdrWriter(buf, CdrEndianness.LittleEndian);
        ParticipantDataSerializer.Write(ref w, src);
        int written = w.Position;

        var r = new CdrReader(buf.AsSpan(0, written), CdrEndianness.LittleEndian);
        var read = ParticipantDataSerializer.Read(ref r);

        read.ProtocolVersion.Should().Be(src.ProtocolVersion);
        read.VendorId.Should().Be(src.VendorId);
        read.Guid.Should().Be(src.Guid);
        read.BuiltinEndpoints.Should().Be(src.BuiltinEndpoints);
        read.LeaseDuration.Should().Be(src.LeaseDuration);
        read.ExpectsInlineQos.Should().Be(src.ExpectsInlineQos);
        read.EntityName.Should().Be(src.EntityName);
        read.MetatrafficUnicastLocators.Should().Equal(src.MetatrafficUnicastLocators);
        read.MetatrafficMulticastLocators.Should().Equal(src.MetatrafficMulticastLocators);
    }

    [Fact]
    public void encap_PL_CDR_LE_を含む完全な_serializedPayload_を構築()
    {
        var src = MakeSampleData();
        var buf = new byte[1024];

        // 4B encap header + PL_CDR
        CdrEncapsulation.Write(buf, CdrEncapsulation.PlCdrLittleEndian);
        var w = new CdrWriter(buf, CdrEndianness.LittleEndian, cdrOrigin: CdrEncapsulation.Size);
        ParticipantDataSerializer.Write(ref w, src);
        int total = w.Position;

        // 先頭 4B が encap header (BE 解釈で 0x0003)
        buf[0].Should().Be((byte)0x00);
        buf[1].Should().Be((byte)0x03); // PL_CDR_LE
        buf[2].Should().Be((byte)0x00); // options
        buf[3].Should().Be((byte)0x00);

        // 続いて最初の PID は 0x0015 (PROTOCOL_VERSION) LE = [0x15, 0x00]
        buf[4].Should().Be((byte)0x15);
        buf[5].Should().Be((byte)0x00);
        buf[6].Should().Be((byte)0x04); // length=4 LE
        buf[7].Should().Be((byte)0x00);

        // Round trip 全体
        var (kind, _) = CdrEncapsulation.Read(buf.AsSpan(0, 4));
        kind.Should().Be(CdrEncapsulation.PlCdrLittleEndian);
        var endian = CdrEncapsulation.GetEndianness(kind);
        var r = new CdrReader(buf.AsSpan(0, total), endian, cdrOrigin: CdrEncapsulation.Size);
        var read = ParticipantDataSerializer.Read(ref r);
        read.Guid.Should().Be(src.Guid);
        read.EntityName.Should().Be(src.EntityName);
    }

    [Fact]
    public void 未知_PID_はスキップされる()
    {
        // PROTOCOL_VERSION の前に未知 PID 0x4242 (length 4) を挟んで構築
        var buf = new byte[256];
        var w = new CdrWriter(buf, CdrEndianness.LittleEndian);
        // unknown PID
        w.WriteUInt16(0x4242);
        w.WriteUInt16(4);
        w.WriteUInt32(0xDEADBEEFu);
        // PROTOCOL_VERSION
        w.WriteUInt16(0x0015);
        w.WriteUInt16(4);
        w.WriteByte(2);
        w.WriteByte(7);
        w.WriteByte(0);
        w.WriteByte(0);
        // SENTINEL
        w.WriteUInt16(0x0001);
        w.WriteUInt16(0);
        int total = w.Position;

        var r = new CdrReader(buf.AsSpan(0, total), CdrEndianness.LittleEndian);
        var data = ParticipantDataSerializer.Read(ref r);
        data.ProtocolVersion.Should().Be(new ProtocolVersion(2, 7));
    }
}
