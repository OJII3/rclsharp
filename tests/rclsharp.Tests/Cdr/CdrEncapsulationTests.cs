using Rclsharp.Cdr;

namespace Rclsharp.Tests.Cdr;

public class CdrEncapsulationTests
{
    [Fact]
    public void Size_は_4()
    {
        CdrEncapsulation.Size.Should().Be(4);
    }

    [Fact]
    public void PlainCdr_は_endian_に応じた_kind_を返す()
    {
        CdrEncapsulation.PlainCdr(CdrEndianness.BigEndian).Should().Be((ushort)0x0000);
        CdrEncapsulation.PlainCdr(CdrEndianness.LittleEndian).Should().Be((ushort)0x0001);
    }

    [Fact]
    public void ParameterListCdr_は_endian_に応じた_kind_を返す()
    {
        CdrEncapsulation.ParameterListCdr(CdrEndianness.BigEndian).Should().Be((ushort)0x0002);
        CdrEncapsulation.ParameterListCdr(CdrEndianness.LittleEndian).Should().Be((ushort)0x0003);
    }

    [Fact]
    public void GetEndianness_は_最下位ビットで判定する()
    {
        CdrEncapsulation.GetEndianness(0x0000).Should().Be(CdrEndianness.BigEndian);
        CdrEncapsulation.GetEndianness(0x0001).Should().Be(CdrEndianness.LittleEndian);
        CdrEncapsulation.GetEndianness(0x0002).Should().Be(CdrEndianness.BigEndian);
        CdrEncapsulation.GetEndianness(0x0003).Should().Be(CdrEndianness.LittleEndian);
    }

    [Fact]
    public void IsParameterList_は_2bit目で判定する()
    {
        CdrEncapsulation.IsParameterList(0x0000).Should().BeFalse();
        CdrEncapsulation.IsParameterList(0x0001).Should().BeFalse();
        CdrEncapsulation.IsParameterList(0x0002).Should().BeTrue();
        CdrEncapsulation.IsParameterList(0x0003).Should().BeTrue();
    }

    [Fact]
    public void Write_と_Read_で往復し_BigEndian_でエンコードされる()
    {
        var buf = new byte[4];
        CdrEncapsulation.Write(buf, CdrEncapsulation.PlCdrLittleEndian, options: 0x1234);

        // kind と options は常に BigEndian
        buf.Should().Equal(0x00, 0x03, 0x12, 0x34);

        var (kind, options) = CdrEncapsulation.Read(buf);
        kind.Should().Be(CdrEncapsulation.PlCdrLittleEndian);
        options.Should().Be((ushort)0x1234);
    }
}
