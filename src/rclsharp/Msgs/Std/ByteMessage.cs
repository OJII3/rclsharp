using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Byte の C# 表現。
/// IDL: <c>byte data</c> (unsigned 8-bit / octet)
/// </summary>
public struct ByteMessage
{
    public const string RosTypeName = "std_msgs/msg/Byte";
    public const string DdsTypeName = "std_msgs::msg::dds_::Byte_";

    public byte Data;

    public ByteMessage(byte data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.Byte(0x{Data:X2})";
}

public sealed class ByteMessageSerializer : ICdrSerializer<ByteMessage>
{
    public static readonly ByteMessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in ByteMessage value) => 1;

    public void Serialize(ref CdrWriter writer, in ByteMessage value)
    {
        writer.WriteByte(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out ByteMessage value)
    {
        value = new ByteMessage(reader.ReadByte());
    }

    public void SerializeKey(ref CdrWriter writer, in ByteMessage value)
    {
    }
}
