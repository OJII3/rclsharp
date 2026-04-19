using Rclsharp.Cdr;

namespace Rclsharp.Msgs.BuiltinInterfaces;

/// <summary>
/// builtin_interfaces/msg/Duration の C# 表現。
/// IDL:
/// <code>
/// int32 sec
/// uint32 nanosec
/// </code>
/// レイアウトは <see cref="Time"/> と同一で、CDR 上は 8 バイト。
/// <para>
/// 注意: <see cref="Rclsharp.Common.Duration"/> は RTPS Duration_t (端数 2^-32 秒) で別物。
/// </para>
/// </summary>
public struct Duration
{
    public const string RosTypeName = "builtin_interfaces/msg/Duration";
    public const string DdsTypeName = "builtin_interfaces::msg::dds_::Duration_";

    public int Sec;
    public uint Nanosec;

    public Duration(int sec, uint nanosec)
    {
        Sec = sec;
        Nanosec = nanosec;
    }

    public override string ToString() => $"Duration({Sec}.{Nanosec:D9})";
}

public sealed class DurationSerializer : ICdrSerializer<Duration>
{
    public static readonly DurationSerializer Instance = new();

    public bool IsKeyed => false;

    public int GetSerializedSize(in Duration value) => 8;

    public void Serialize(ref CdrWriter writer, in Duration value)
    {
        writer.WriteInt32(value.Sec);
        writer.WriteUInt32(value.Nanosec);
    }

    public void Deserialize(ref CdrReader reader, out Duration value)
    {
        int sec = reader.ReadInt32();
        uint nanosec = reader.ReadUInt32();
        value = new Duration(sec, nanosec);
    }

    public void SerializeKey(ref CdrWriter writer, in Duration value)
    {
    }
}
