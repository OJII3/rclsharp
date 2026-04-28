using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/ColorRGBA の C# 表現。
/// IDL:
/// <code>
/// float32 r
/// float32 g
/// float32 b
/// float32 a
/// </code>
/// CDR 上は 4 バイト境界 × 4 = 16 バイト。
/// </summary>
public struct ColorRgba
{
    public const string RosTypeName = "std_msgs/msg/ColorRGBA";
    public const string DdsTypeName = "std_msgs::msg::dds_::ColorRGBA_";

    public float R;
    public float G;
    public float B;
    public float A;

    public ColorRgba(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public override string ToString() => $"ColorRGBA(r={R}, g={G}, b={B}, a={A})";
}

public sealed class ColorRgbaSerializer : ICdrSerializer<ColorRgba>
{
    public static readonly ColorRgbaSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in ColorRgba value) => 16;

    public void Serialize(ref CdrWriter writer, in ColorRgba value)
    {
        writer.WriteFloat(value.R);
        writer.WriteFloat(value.G);
        writer.WriteFloat(value.B);
        writer.WriteFloat(value.A);
    }

    public void Deserialize(ref CdrReader reader, out ColorRgba value)
    {
        float r = reader.ReadFloat();
        float g = reader.ReadFloat();
        float b = reader.ReadFloat();
        float a = reader.ReadFloat();
        value = new ColorRgba(r, g, b, a);
    }

    public void SerializeKey(ref CdrWriter writer, in ColorRgba value)
    {
    }
}
