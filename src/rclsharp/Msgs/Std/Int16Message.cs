using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Int16 の C# 表現。IDL: <c>int16 data</c>。CDR 上は 2 バイト境界。
/// </summary>
public struct Int16Message
{
    public const string RosTypeName = "std_msgs/msg/Int16";
    public const string DdsTypeName = "std_msgs::msg::dds_::Int16_";

    public short Data;

    public Int16Message(short data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.Int16({Data})";
}

public sealed class Int16MessageSerializer : ICdrSerializer<Int16Message>
{
    public static readonly Int16MessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in Int16Message value) => 2;

    public void Serialize(ref CdrWriter writer, in Int16Message value)
    {
        writer.WriteInt16(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out Int16Message value)
    {
        value = new Int16Message(reader.ReadInt16());
    }

    public void SerializeKey(ref CdrWriter writer, in Int16Message value)
    {
    }
}
