using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Empty の C# 表現。フィールドなし。
/// CDR 上は 1 バイトの構造体ダミー (rosidl は空構造体に 1 バイトのプレースホルダを入れる慣習)。
/// </summary>
public struct EmptyMessage
{
    public const string RosTypeName = "std_msgs/msg/Empty";
    public const string DdsTypeName = "std_msgs::msg::dds_::Empty_";

    public override string ToString() => "std_msgs.Empty()";
}

/// <summary>
/// <see cref="EmptyMessage"/> の CDR シリアライザ。
/// rosidl が空メッセージに追加する 1 バイトのダミー (構造体の最小サイズ保証) を読み書きする。
/// </summary>
public sealed class EmptyMessageSerializer : ICdrSerializer<EmptyMessage>
{
    public static readonly EmptyMessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in EmptyMessage value) => 1;

    public void Serialize(ref CdrWriter writer, in EmptyMessage value)
    {
        // rosidl の Empty は 1 バイトの structure dummy (0x00) を持つ
        writer.WriteByte(0);
    }

    public void Deserialize(ref CdrReader reader, out EmptyMessage value)
    {
        _ = reader.ReadByte();
        value = default;
    }

    public void SerializeKey(ref CdrWriter writer, in EmptyMessage value)
    {
    }
}
