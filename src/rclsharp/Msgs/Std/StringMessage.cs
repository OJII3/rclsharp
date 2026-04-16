using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/String の C# 表現。
/// IDL: <c>string data</c>
/// DDS 型名: <c>std_msgs::msg::dds_::String_</c>
/// </summary>
public struct StringMessage
{
    /// <summary>ROS 2 型名 (mangling 前)。</summary>
    public const string RosTypeName = "std_msgs/msg/String";

    /// <summary>DDS 型名 (mangling 後、SEDP DiscoveredWriterData/ReaderData に載せる名前)。</summary>
    public const string DdsTypeName = "std_msgs::msg::dds_::String_";

    public string Data;

    public StringMessage(string data)
    {
        Data = data;
    }

    public override string ToString() => $"std_msgs.String(\"{Data}\")";
}

/// <summary>
/// <see cref="StringMessage"/> の <see cref="ICdrSerializer{T}"/>。
/// 中身は <c>WriteString(data)</c> のみ。Keyed ではない。
/// </summary>
public sealed class StringMessageSerializer : ICdrSerializer<StringMessage>
{
    public static readonly StringMessageSerializer Instance = new();

    public bool IsKeyed => false;

    /// <summary>UTF-8 サイズ + 5 (length 4B + NUL 1B) を概算返す。</summary>
    public int GetSerializedSize(in StringMessage value)
    {
        int strLen = value.Data is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(value.Data);
        return 4 + strLen + 1;
    }

    public void Serialize(ref CdrWriter writer, in StringMessage value)
    {
        writer.WriteString(value.Data);
    }

    public void Deserialize(ref CdrReader reader, out StringMessage value)
    {
        value = new StringMessage(reader.ReadString());
    }

    public void SerializeKey(ref CdrWriter writer, in StringMessage value)
    {
        // Not keyed
    }
}
