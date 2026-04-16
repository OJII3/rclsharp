using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Int32 の C# 表現。IDL: <c>int32 data</c>。CDR 上は 4 バイト境界。
/// </summary>
public struct Int32Message
{
    public const string RosTypeName = "std_msgs/msg/Int32";
    public const string DdsTypeName = "std_msgs::msg::dds_::Int32_";

    public int Data;

    public Int32Message(int data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.Int32({Data})";
}

public sealed class Int32MessageSerializer : ICdrSerializer<Int32Message>
{
    public static readonly Int32MessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in Int32Message value) => 4;

    public void Serialize(ref CdrWriter writer, in Int32Message value)
    {
        writer.WriteInt32(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out Int32Message value)
    {
        value = new Int32Message(reader.ReadInt32());
    }

    public void SerializeKey(ref CdrWriter writer, in Int32Message value)
    {
    }
}
