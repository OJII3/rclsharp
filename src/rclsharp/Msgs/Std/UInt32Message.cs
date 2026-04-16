using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/UInt32 の C# 表現。IDL: <c>uint32 data</c>。CDR 上は 4 バイト境界。
/// </summary>
public struct UInt32Message
{
    public const string RosTypeName = "std_msgs/msg/UInt32";
    public const string DdsTypeName = "std_msgs::msg::dds_::UInt32_";

    public uint Data;

    public UInt32Message(uint data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.UInt32({Data})";
}

public sealed class UInt32MessageSerializer : ICdrSerializer<UInt32Message>
{
    public static readonly UInt32MessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in UInt32Message value) => 4;

    public void Serialize(ref CdrWriter writer, in UInt32Message value)
    {
        writer.WriteUInt32(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out UInt32Message value)
    {
        value = new UInt32Message(reader.ReadUInt32());
    }

    public void SerializeKey(ref CdrWriter writer, in UInt32Message value)
    {
    }
}
