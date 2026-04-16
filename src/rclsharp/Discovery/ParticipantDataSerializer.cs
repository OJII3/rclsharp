using Rclsharp.Cdr;
using Rclsharp.Cdr.ParameterList;
using Rclsharp.Common;

using Guid = Rclsharp.Common.Guid;

namespace Rclsharp.Discovery;

/// <summary>
/// <see cref="ParticipantData"/> を PL_CDR ParameterList としてシリアライズ/デシリアライズする。
/// 出力には CDR エンキャプスレーションヘッダ (PL_CDR_LE/BE) は含まない (Data submessage 構築側の責務)。
/// </summary>
public static class ParticipantDataSerializer
{
    /// <summary>
    /// data を PL_CDR ParameterList として書き込む (SENTINEL 含む)。
    /// </summary>
    public static void Write(ref CdrWriter writer, ParticipantData data)
    {
        var pl = new ParameterListWriter(writer);
        bool littleEndian = writer.Endianness == CdrEndianness.LittleEndian;

        // PROTOCOL_VERSION (2B + 2B pad)
        pl.BeginParameter(ParameterId.ProtocolVersion);
        pl.Inner.WriteByte(data.ProtocolVersion.Major);
        pl.Inner.WriteByte(data.ProtocolVersion.Minor);
        pl.EndParameter();

        // VENDORID (2B + 2B pad)
        pl.BeginParameter(ParameterId.VendorId);
        pl.Inner.WriteByte(data.VendorId.V0);
        pl.Inner.WriteByte(data.VendorId.V1);
        pl.EndParameter();

        // PARTICIPANT_GUID (16B)
        pl.BeginParameter(ParameterId.ParticipantGuid);
        var guidBytes = new byte[Guid.Size];
        data.Guid.WriteTo(guidBytes);
        pl.Inner.WriteRawBytes(guidBytes);
        pl.EndParameter();

        // BUILTIN_ENDPOINT_SET (4B)
        pl.BeginParameter(ParameterId.BuiltinEndpointSet);
        pl.Inner.WriteUInt32((uint)data.BuiltinEndpoints);
        pl.EndParameter();

        // PARTICIPANT_LEASE_DURATION (8B Duration_t)
        pl.BeginParameter(ParameterId.ParticipantLeaseDuration);
        var durationBytes = new byte[Duration.Size];
        data.LeaseDuration.WriteTo(durationBytes, littleEndian);
        pl.Inner.WriteRawBytes(durationBytes);
        pl.EndParameter();

        // EXPECTS_INLINE_QOS (1B bool + 3B pad)
        pl.BeginParameter(ParameterId.ExpectsInlineQos);
        pl.Inner.WriteBool(data.ExpectsInlineQos);
        pl.EndParameter();

        WriteLocators(ref pl, ParameterId.MetatrafficUnicastLocator, data.MetatrafficUnicastLocators, littleEndian);
        WriteLocators(ref pl, ParameterId.MetatrafficMulticastLocator, data.MetatrafficMulticastLocators, littleEndian);
        WriteLocators(ref pl, ParameterId.DefaultUnicastLocator, data.DefaultUnicastLocators, littleEndian);
        WriteLocators(ref pl, ParameterId.DefaultMulticastLocator, data.DefaultMulticastLocators, littleEndian);

        // ENTITY_NAME (任意)
        if (!string.IsNullOrEmpty(data.EntityName))
        {
            pl.BeginParameter(ParameterId.EntityName);
            pl.Inner.WriteString(data.EntityName);
            pl.EndParameter();
        }

        pl.WriteSentinel();

        // pl.Inner は ref で同じ writer を指しているので位置は同期している
        writer = pl.Inner;
    }

    private static void WriteLocators(ref ParameterListWriter pl, ushort pid, List<Locator> locators, bool littleEndian)
    {
        foreach (var loc in locators)
        {
            pl.BeginParameter(pid);
            var bytes = new byte[Locator.Size];
            loc.WriteTo(bytes, littleEndian);
            pl.Inner.WriteRawBytes(bytes);
            pl.EndParameter();
        }
    }

    /// <summary>
    /// PL_CDR ParameterList を読み出して <see cref="ParticipantData"/> を生成する。
    /// 未知 PID はスキップ (ParameterListReader が自動で進める)。
    /// </summary>
    public static ParticipantData Read(ref CdrReader reader)
    {
        var data = new ParticipantData();
        var pl = new ParameterListReader(reader);
        bool littleEndian = reader.Endianness == CdrEndianness.LittleEndian;

        while (pl.MoveNext(out var pid, out var length))
        {
            switch (ParameterId.StripFlags(pid))
            {
                case ParameterId.ProtocolVersion:
                    data.ProtocolVersion = new ProtocolVersion(pl.Inner.ReadByte(), pl.Inner.ReadByte());
                    break;

                case ParameterId.VendorId:
                    data.VendorId = new VendorId(pl.Inner.ReadByte(), pl.Inner.ReadByte());
                    break;

                case ParameterId.ParticipantGuid:
                    {
                        var raw = pl.CurrentValueRaw();
                        if (raw.Length >= Guid.Size)
                        {
                            data.Guid = Guid.Read(raw[..Guid.Size]);
                        }
                        break;
                    }

                case ParameterId.BuiltinEndpointSet:
                    data.BuiltinEndpoints = (BuiltinEndpointSet)pl.Inner.ReadUInt32();
                    break;

                case ParameterId.ParticipantLeaseDuration:
                    {
                        var raw = pl.CurrentValueRaw();
                        if (raw.Length >= Duration.Size)
                        {
                            data.LeaseDuration = Duration.Read(raw[..Duration.Size], littleEndian);
                        }
                        break;
                    }

                case ParameterId.ExpectsInlineQos:
                    data.ExpectsInlineQos = pl.Inner.ReadBool();
                    break;

                case ParameterId.MetatrafficUnicastLocator:
                    AppendLocator(pl.CurrentValueRaw(), littleEndian, data.MetatrafficUnicastLocators);
                    break;
                case ParameterId.MetatrafficMulticastLocator:
                    AppendLocator(pl.CurrentValueRaw(), littleEndian, data.MetatrafficMulticastLocators);
                    break;
                case ParameterId.DefaultUnicastLocator:
                    AppendLocator(pl.CurrentValueRaw(), littleEndian, data.DefaultUnicastLocators);
                    break;
                case ParameterId.DefaultMulticastLocator:
                    AppendLocator(pl.CurrentValueRaw(), littleEndian, data.DefaultMulticastLocators);
                    break;

                case ParameterId.EntityName:
                    data.EntityName = pl.Inner.ReadString();
                    break;

                default:
                    // 未知 PID は MoveNext が次へ進める際に自動スキップ
                    break;
            }
        }

        reader = pl.Inner;
        return data;
    }

    private static void AppendLocator(ReadOnlySpan<byte> raw, bool littleEndian, List<Locator> dest)
    {
        if (raw.Length >= Locator.Size)
        {
            dest.Add(Locator.Read(raw[..Locator.Size], littleEndian));
        }
    }
}
