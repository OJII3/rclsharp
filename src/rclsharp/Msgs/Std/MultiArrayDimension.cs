using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/MultiArrayDimension の C# 表現。
/// IDL:
/// <code>
/// string label
/// uint32 size
/// uint32 stride
/// </code>
/// </summary>
public struct MultiArrayDimension
{
    public const string RosTypeName = "std_msgs/msg/MultiArrayDimension";
    public const string DdsTypeName = "std_msgs::msg::dds_::MultiArrayDimension_";

    public string Label;
    public uint Size;
    public uint Stride;

    public MultiArrayDimension(string label, uint size, uint stride)
    {
        Label = label;
        Size = size;
        Stride = stride;
    }

    public override string ToString() => $"Dim(label=\"{Label}\", size={Size}, stride={Stride})";
}

public sealed class MultiArrayDimensionSerializer : ICdrSerializer<MultiArrayDimension>
{
    public static readonly MultiArrayDimensionSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in MultiArrayDimension value)
    {
        int labelLen = value.Label is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(value.Label);
        // string: 4 + labelLen + 1, 最大 3 バイトの alignment padding、その後 uint32 x 2
        return 4 + labelLen + 1 + 3 + 4 + 4;
    }

    public void Serialize(ref CdrWriter writer, in MultiArrayDimension value)
    {
        writer.WriteString(value.Label);
        writer.WriteUInt32(value.Size);
        writer.WriteUInt32(value.Stride);
    }

    public void Deserialize(ref CdrReader reader, out MultiArrayDimension value)
    {
        string label = reader.ReadString();
        uint size = reader.ReadUInt32();
        uint stride = reader.ReadUInt32();
        value = new MultiArrayDimension(label, size, stride);
    }

    public void SerializeKey(ref CdrWriter writer, in MultiArrayDimension value)
    {
    }
}
