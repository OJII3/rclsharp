using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Float32 の C# 表現。IDL: <c>float32 data</c>。CDR 上は 4 バイト境界。
/// </summary>
public struct Float32Message
{
    public const string RosTypeName = "std_msgs/msg/Float32";
    public const string DdsTypeName = "std_msgs::msg::dds_::Float32_";

    public float Data;

    public Float32Message(float data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.Float32({Data})";
}

public sealed class Float32MessageSerializer : ICdrSerializer<Float32Message>
{
    public static readonly Float32MessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in Float32Message value) => 4;

    public void Serialize(ref CdrWriter writer, in Float32Message value)
    {
        writer.WriteFloat(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out Float32Message value)
    {
        value = new Float32Message(reader.ReadFloat());
    }

    public void SerializeKey(ref CdrWriter writer, in Float32Message value)
    {
    }
}
