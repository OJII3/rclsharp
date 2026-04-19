using Rclsharp.Cdr;

namespace Rclsharp.Msgs.BuiltinInterfaces;

/// <summary>
/// builtin_interfaces/msg/Time の C# 表現。
/// IDL:
/// <code>
/// int32 sec
/// uint32 nanosec
/// </code>
/// CDR 上は 4 バイト境界 × 2 = 8 バイト。
/// Unix エポック基準の時刻を秒とナノ秒で表す。
/// <para>
/// 注意: <see cref="Rclsharp.Common.Time"/> は RTPS Time_t (端数 2^-32 秒) で別物。
/// </para>
/// </summary>
public struct Time
{
    public const string RosTypeName = "builtin_interfaces/msg/Time";
    public const string DdsTypeName = "builtin_interfaces::msg::dds_::Time_";

    public int Sec;
    public uint Nanosec;

    public Time(int sec, uint nanosec)
    {
        Sec = sec;
        Nanosec = nanosec;
    }

    public override string ToString() => $"Time({Sec}.{Nanosec:D9})";
}

public sealed class TimeSerializer : ICdrSerializer<Time>
{
    public static readonly TimeSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in Time value) => 8;

    public void Serialize(ref CdrWriter writer, in Time value)
    {
        writer.WriteInt32(value.Sec);
        writer.WriteUInt32(value.Nanosec);
    }

    public void Deserialize(ref CdrReader reader, out Time value)
    {
        int sec = reader.ReadInt32();
        uint nanosec = reader.ReadUInt32();
        value = new Time(sec, nanosec);
    }

    public void SerializeKey(ref CdrWriter writer, in Time value)
    {
    }
}
