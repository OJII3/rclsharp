using Rclsharp.Cdr;
using Rclsharp.Msgs.BuiltinInterfaces;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/Header の C# 表現。
/// IDL:
/// <code>
/// builtin_interfaces/Time stamp
/// string frame_id
/// </code>
/// ROS 2 では ROS 1 にあった <c>uint32 seq</c> は削除されているため持たない。
/// </summary>
public struct HeaderMessage
{
    public const string RosTypeName = "std_msgs/msg/Header";
    public const string DdsTypeName = "std_msgs::msg::dds_::Header_";

    public Time Stamp;
    public string FrameId;

    public HeaderMessage(Time stamp, string frameId)
    {
        Stamp = stamp;
        FrameId = frameId;
    }

    public override string ToString() => $"Header(stamp={Stamp}, frame_id=\"{FrameId}\")";
}

public sealed class HeaderMessageSerializer : ICdrSerializer<HeaderMessage>
{
    public static readonly HeaderMessageSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in HeaderMessage value)
    {
        // stamp(8) + length(4) + utf8 + NUL(1)。他 msg に埋め込まれた際の
        // 先頭 4-align パディングは上限計算で省略 (MultiArrayLayout と同方針)。
        int strLen = value.FrameId is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(value.FrameId);
        return 8 + 4 + strLen + 1;
    }

    public void Serialize(ref CdrWriter writer, in HeaderMessage value)
    {
        TimeSerializer.Instance.Serialize(ref writer, in value.Stamp);
        writer.WriteString(value.FrameId);
    }

    public void Deserialize(ref CdrReader reader, out HeaderMessage value)
    {
        TimeSerializer.Instance.Deserialize(ref reader, out Time stamp);
        string frameId = reader.ReadString();
        value = new HeaderMessage(stamp, frameId);
    }

    public void SerializeKey(ref CdrWriter writer, in HeaderMessage value)
    {
    }
}
