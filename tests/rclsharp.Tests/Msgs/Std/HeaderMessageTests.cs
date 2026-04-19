using Rclsharp.Cdr;
using Rclsharp.Msgs.BuiltinInterfaces;
using Rclsharp.Msgs.Std;

namespace Rclsharp.Tests.Msgs.Std;

/// <summary>
/// std_msgs/Header の CDR シリアライズテスト。
/// ROS 2 版のため <c>seq</c> は持たず、stamp (Time) + frame_id (string) のみ。
/// </summary>
public class HeaderMessageTests
{
    private static byte[] Serialize<T>(ICdrSerializer<T> serializer, in T value, CdrEndianness endian)
    {
        Span<byte> buf = stackalloc byte[256];
        var w = new CdrWriter(buf, endian);
        serializer.Serialize(ref w, in value);
        return buf[..w.BytesWritten].ToArray();
    }

    private static T Deserialize<T>(ICdrSerializer<T> serializer, ReadOnlySpan<byte> bytes, CdrEndianness endian)
    {
        var r = new CdrReader(bytes, endian);
        serializer.Deserialize(ref r, out T value);
        return value;
    }

    [Fact]
    public void Header_LE_bit_exact()
    {
        var h = new HeaderMessage(new Time(1, 2u), "x");
        Serialize(HeaderMessageSerializer.Instance, in h, CdrEndianness.LittleEndian)
            .Should().Equal(
                0x01, 0x00, 0x00, 0x00,            // sec = 1
                0x02, 0x00, 0x00, 0x00,            // nanosec = 2
                0x02, 0x00, 0x00, 0x00,            // frame_id length (incl NUL) = 2
                (byte)'x', 0x00);                  // "x\0"
    }

    [Fact]
    public void Header_空文字列_LE_bit_exact()
    {
        var h = new HeaderMessage(new Time(0, 0u), string.Empty);
        Serialize(HeaderMessageSerializer.Instance, in h, CdrEndianness.LittleEndian)
            .Should().Equal(
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00,            // length = 1 (NUL only)
                0x00);                             // "\0"
    }

    [Theory]
    [InlineData(CdrEndianness.LittleEndian)]
    [InlineData(CdrEndianness.BigEndian)]
    public void Header_roundtrip(CdrEndianness endian)
    {
        var h = new HeaderMessage(new Time(1_700_000_000, 123_456_789u), "base_link");
        var bytes = Serialize(HeaderMessageSerializer.Instance, in h, endian);
        var read = Deserialize(HeaderMessageSerializer.Instance, bytes, endian);
        read.Stamp.Sec.Should().Be(1_700_000_000);
        read.Stamp.Nanosec.Should().Be(123_456_789u);
        read.FrameId.Should().Be("base_link");
    }

    [Theory]
    [InlineData(CdrEndianness.LittleEndian)]
    [InlineData(CdrEndianness.BigEndian)]
    public void Header_マルチバイト文字_roundtrip(CdrEndianness endian)
    {
        var h = new HeaderMessage(new Time(0, 0u), "日本語フレーム");
        var bytes = Serialize(HeaderMessageSerializer.Instance, in h, endian);
        var read = Deserialize(HeaderMessageSerializer.Instance, bytes, endian);
        read.FrameId.Should().Be("日本語フレーム");
    }
}
