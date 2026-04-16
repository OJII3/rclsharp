using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Float64 の C# 表現。IDL: <c>float64 data</c>。CDR 上は 8 バイト境界。
/// </summary>
public struct Float64Message
{
    public const string RosTypeName = "std_msgs/msg/Float64";
    public const string DdsTypeName = "std_msgs::msg::dds_::Float64_";

    public double Data;

    public Float64Message(double data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.Float64({Data})";
}

public sealed class Float64MessageSerializer : ICdrSerializer<Float64Message>
{
    public static readonly Float64MessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in Float64Message value) => 8;

    public void Serialize(ref CdrWriter writer, in Float64Message value)
    {
        writer.WriteDouble(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out Float64Message value)
    {
        value = new Float64Message(reader.ReadDouble());
    }

    public void SerializeKey(ref CdrWriter writer, in Float64Message value)
    {
    }
}
