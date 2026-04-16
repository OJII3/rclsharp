using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Int8 の C# 表現。IDL: <c>int8 data</c>。
/// </summary>
public struct Int8Message
{
    public const string RosTypeName = "std_msgs/msg/Int8";
    public const string DdsTypeName = "std_msgs::msg::dds_::Int8_";

    public sbyte Data;

    public Int8Message(sbyte data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.Int8({Data})";
}

public sealed class Int8MessageSerializer : ICdrSerializer<Int8Message>
{
    public static readonly Int8MessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in Int8Message value) => 1;

    public void Serialize(ref CdrWriter writer, in Int8Message value)
    {
        writer.WriteSByte(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out Int8Message value)
    {
        value = new Int8Message(reader.ReadSByte());
    }

    public void SerializeKey(ref CdrWriter writer, in Int8Message value)
    {
    }
}
