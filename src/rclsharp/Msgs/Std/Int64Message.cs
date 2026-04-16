using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Int64 の C# 表現。IDL: <c>int64 data</c>。CDR 上は 8 バイト境界。
/// </summary>
public struct Int64Message
{
    public const string RosTypeName = "std_msgs/msg/Int64";
    public const string DdsTypeName = "std_msgs::msg::dds_::Int64_";

    public long Data;

    public Int64Message(long data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.Int64({Data})";
}

public sealed class Int64MessageSerializer : ICdrSerializer<Int64Message>
{
    public static readonly Int64MessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in Int64Message value) => 8;

    public void Serialize(ref CdrWriter writer, in Int64Message value)
    {
        writer.WriteInt64(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out Int64Message value)
    {
        value = new Int64Message(reader.ReadInt64());
    }

    public void SerializeKey(ref CdrWriter writer, in Int64Message value)
    {
    }
}
