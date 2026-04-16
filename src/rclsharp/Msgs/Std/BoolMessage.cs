using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Bool の C# 表現。
/// IDL: <c>bool data</c>
/// CDR 上は 1 バイト (0x00 / 0x01)。
/// </summary>
public struct BoolMessage
{
    public const string RosTypeName = "std_msgs/msg/Bool";
    public const string DdsTypeName = "std_msgs::msg::dds_::Bool_";

    public bool Data;

    public BoolMessage(bool data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.Bool({Data})";
}

public sealed class BoolMessageSerializer : ICdrSerializer<BoolMessage>
{
    public static readonly BoolMessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in BoolMessage value) => 1;

    public void Serialize(ref CdrWriter writer, in BoolMessage value)
    {
        writer.WriteBool(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out BoolMessage value)
    {
        value = new BoolMessage(reader.ReadBool());
    }

    public void SerializeKey(ref CdrWriter writer, in BoolMessage value)
    {
    }
}
