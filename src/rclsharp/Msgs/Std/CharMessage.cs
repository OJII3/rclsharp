using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Char の C# 表現。
/// IDL: <c>char data</c> (signed 8-bit; ROS 2 では非推奨、uint8 推奨)。
/// </summary>
public struct CharMessage
{
    public const string RosTypeName = "std_msgs/msg/Char";
    public const string DdsTypeName = "std_msgs::msg::dds_::Char_";

    public sbyte Data;

    public CharMessage(sbyte data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.Char({Data})";
}

public sealed class CharMessageSerializer : ICdrSerializer<CharMessage>
{
    public static readonly CharMessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in CharMessage value) => 1;

    public void Serialize(ref CdrWriter writer, in CharMessage value)
    {
        writer.WriteSByte(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out CharMessage value)
    {
        value = new CharMessage(reader.ReadSByte());
    }

    public void SerializeKey(ref CdrWriter writer, in CharMessage value)
    {
    }
}
