using Rclsharp.Cdr;

namespace Rclsharp.Msgs.Std;

/// <summary>
/// std_msgs/msg/MultiArrayLayout の C# 表現。
/// IDL:
/// <code>
/// std_msgs/MultiArrayDimension[] dim
/// uint32 data_offset
/// </code>
/// </summary>
public struct MultiArrayLayout
{
    public const string RosTypeName = "std_msgs/msg/MultiArrayLayout";
    public const string DdsTypeName = "std_msgs::msg::dds_::MultiArrayLayout_";

    public MultiArrayDimension[] Dim;
    public uint DataOffset;

    public MultiArrayLayout(MultiArrayDimension[] dim, uint dataOffset)
    {
        Dim = dim;
        DataOffset = dataOffset;
    }

    public override string ToString() =>
        $"Layout(dim=[{(Dim is null ? 0 : Dim.Length)}], data_offset={DataOffset})";
}

public sealed class MultiArrayLayoutSerializer : ICdrSerializer<MultiArrayLayout>
{
    public static readonly MultiArrayLayoutSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in MultiArrayLayout value)
    {
        int total = 4; // sequence length
        if (value.Dim is not null)
        {
            var dimSer = MultiArrayDimensionSerializer.Instance;
            foreach (ref readonly var d in value.Dim.AsSpan())
            {
                total += dimSer.GetSerializedSize(in d);
            }
        }
        total += 4; // data_offset (max align pad は 4 以下だが上限計算で省略)
        return total + 4;
    }

    public void Serialize(ref CdrWriter writer, in MultiArrayLayout value)
    {
        var dim = value.Dim ?? [];
        writer.WriteSequenceLength(dim.Length);
        var dimSer = MultiArrayDimensionSerializer.Instance;
        foreach (ref readonly var d in dim.AsSpan())
        {
            dimSer.Serialize(ref writer, in d);
        }
        writer.WriteUInt32(value.DataOffset);
    }

    public void Deserialize(ref CdrReader reader, out MultiArrayLayout value)
    {
        int count = reader.ReadSequenceLength();
        var dim = count == 0 ? [] : new MultiArrayDimension[count];
        var dimSer = MultiArrayDimensionSerializer.Instance;
        for (int i = 0; i < count; i++)
        {
            dimSer.Deserialize(ref reader, out dim[i]);
        }
        uint dataOffset = reader.ReadUInt32();
        value = new MultiArrayLayout(dim, dataOffset);
    }

    public void SerializeKey(ref CdrWriter writer, in MultiArrayLayout value)
    {
    }
}
