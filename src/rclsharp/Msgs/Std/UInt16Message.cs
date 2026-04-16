using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/UInt16 の C# 表現。IDL: <c>uint16 data</c>。CDR 上は 2 バイト境界。
/// </summary>
public struct UInt16Message
{
    public const string RosTypeName = "std_msgs/msg/UInt16";
    public const string DdsTypeName = "std_msgs::msg::dds_::UInt16_";

    public ushort Data;

    public UInt16Message(ushort data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.UInt16({Data})";
}

public sealed class UInt16MessageSerializer : ICdrSerializer<UInt16Message>
{
    public static readonly UInt16MessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in UInt16Message value) => 2;

    public void Serialize(ref CdrWriter writer, in UInt16Message value)
    {
        writer.WriteUInt16(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out UInt16Message value)
    {
        value = new UInt16Message(reader.ReadUInt16());
    }

    public void SerializeKey(ref CdrWriter writer, in UInt16Message value)
    {
    }
}
