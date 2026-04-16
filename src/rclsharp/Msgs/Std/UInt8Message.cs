using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/UInt8 の C# 表現。IDL: <c>uint8 data</c>。
/// </summary>
public struct UInt8Message
{
    public const string RosTypeName = "std_msgs/msg/UInt8";
    public const string DdsTypeName = "std_msgs::msg::dds_::UInt8_";

    public byte Data;

    public UInt8Message(byte data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.UInt8({Data})";
}

public sealed class UInt8MessageSerializer : ICdrSerializer<UInt8Message>
{
    public static readonly UInt8MessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in UInt8Message value) => 1;

    public void Serialize(ref CdrWriter writer, in UInt8Message value)
    {
        writer.WriteByte(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out UInt8Message value)
    {
        value = new UInt8Message(reader.ReadByte());
    }

    public void SerializeKey(ref CdrWriter writer, in UInt8Message value)
    {
    }
}
