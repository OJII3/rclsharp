using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/UInt64 の C# 表現。IDL: <c>uint64 data</c>。CDR 上は 8 バイト境界。
/// </summary>
public struct UInt64Message
{
    public const string RosTypeName = "std_msgs/msg/UInt64";
    public const string DdsTypeName = "std_msgs::msg::dds_::UInt64_";

    public ulong Data;

    public UInt64Message(ulong data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.UInt64({Data})";
}

public sealed class UInt64MessageSerializer : ICdrSerializer<UInt64Message>
{
    public static readonly UInt64MessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in UInt64Message value) => 8;

    public void Serialize(ref CdrWriter writer, in UInt64Message value)
    {
        writer.WriteUInt64(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out UInt64Message value)
    {
        value = new UInt64Message(reader.ReadUInt64());
    }

    public void SerializeKey(ref CdrWriter writer, in UInt64Message value)
    {
    }
}
