using Rclsharp.Cdr;
using Rclsharp.Cdr.ParameterList;
using Rclsharp.Common;
using Rclsharp.Dds.QoS;

using Guid = Rclsharp.Common.Guid;

namespace Rclsharp.Discovery;

/// <summary>
/// <see cref="DiscoveredEndpointData"/> を PL_CDR ParameterList に変換する。
/// 出力には CDR エンキャプスレーションヘッダ (PL_CDR_LE/BE) は含まない (Data submessage 構築側の責務)。
/// </summary>
public static class DiscoveredEndpointDataSerializer
{
    /// <summary>data を PL_CDR ParameterList として書き込む (SENTINEL 含む)。</summary>
    public static void Write(ref CdrWriter writer, DiscoveredEndpointData data)
    {
        var pl = new ParameterListWriter(writer);
        bool littleEndian = writer.Endianness == CdrEndianness.LittleEndian;

        // PARTICIPANT_GUID (16B)
        pl.BeginParameter(ParameterId.ParticipantGuid);
        var pgBytes = new byte[Guid.Size];
        data.ParticipantGuid.WriteTo(pgBytes);
        pl.WriteRawBytes(pgBytes);
        pl.EndParameter();

        // ENDPOINT_GUID (16B)
        pl.BeginParameter(ParameterId.EndpointGuid);
        var egBytes = new byte[Guid.Size];
        data.EndpointGuid.WriteTo(egBytes);
        pl.WriteRawBytes(egBytes);
        pl.EndParameter();

        // KEY_HASH (16B = endpoint guid)
        pl.BeginParameter(ParameterId.KeyHash);
        pl.WriteRawBytes(egBytes);
        pl.EndParameter();

        // TOPIC_NAME (string)
        pl.BeginParameter(ParameterId.TopicName);
        pl.WriteString(data.TopicName);
        pl.EndParameter();

        // TYPE_NAME (string)
        pl.BeginParameter(ParameterId.TypeName);
        pl.WriteString(data.TypeName);
        pl.EndParameter();

        // RELIABILITY (4B kind + 8B Duration = 12B)
        pl.BeginParameter(ParameterId.Reliability);
        pl.WriteInt32((int)data.Reliability.Kind);
        var blockingBytes = new byte[Duration.Size];
        data.Reliability.MaxBlockingTime.WriteTo(blockingBytes, littleEndian);
        pl.WriteRawBytes(blockingBytes);
        pl.EndParameter();

        // DURABILITY (4B kind)
        pl.BeginParameter(ParameterId.Durability);
        pl.WriteInt32((int)data.Durability.Kind);
        pl.EndParameter();

        // UNICAST_LOCATOR (24B each)
        foreach (var loc in data.UnicastLocators)
        {
            pl.BeginParameter(ParameterId.UnicastLocator);
            var locBytes = new byte[Locator.Size];
            loc.WriteTo(locBytes, littleEndian);
            pl.WriteRawBytes(locBytes);
            pl.EndParameter();
        }

        // MULTICAST_LOCATOR (24B each)
        foreach (var loc in data.MulticastLocators)
        {
            pl.BeginParameter(ParameterId.MulticastLocator);
            var locBytes = new byte[Locator.Size];
            loc.WriteTo(locBytes, littleEndian);
            pl.WriteRawBytes(locBytes);
            pl.EndParameter();
        }

        pl.WriteSentinel();
        writer = pl.CurrentWriter;
    }

    /// <summary>PL_CDR ParameterList を読み出して <see cref="DiscoveredEndpointData"/> を生成する。</summary>
    public static DiscoveredEndpointData Read(ref CdrReader reader, EndpointKind kind)
    {
        var data = new DiscoveredEndpointData { Kind = kind };
        var pl = new ParameterListReader(reader);
        bool littleEndian = reader.Endianness == CdrEndianness.LittleEndian;

        while (pl.MoveNext(out var pid, out var length))
        {
            switch (ParameterId.StripFlags(pid))
            {
                case ParameterId.ParticipantGuid:
                    {
                        var raw = pl.CurrentValueRaw();
                        if (raw.Length >= Guid.Size)
                        {
                            data.ParticipantGuid = Guid.Read(raw[..Guid.Size]);
                        }
                        break;
                    }
                case ParameterId.EndpointGuid:
                    {
                        var raw = pl.CurrentValueRaw();
                        if (raw.Length >= Guid.Size)
                        {
                            data.EndpointGuid = Guid.Read(raw[..Guid.Size]);
                        }
                        break;
                    }
                case ParameterId.TopicName:
                    data.TopicName = pl.ReadString();
                    break;
                case ParameterId.TypeName:
                    data.TypeName = pl.ReadString();
                    break;
                case ParameterId.Reliability:
                    {
                        var rkind = (ReliabilityKind)pl.ReadInt32();
                        var raw = pl.CurrentValueRaw();
                        // 残り 8B が Duration
                        var blocking = raw.Length >= 12
                            ? Duration.Read(raw.Slice(4, 8), littleEndian)
                            : Duration.Zero;
                        data.Reliability = new ReliabilityQos(rkind, blocking);
                        break;
                    }
                case ParameterId.Durability:
                    data.Durability = new DurabilityQos((DurabilityKind)pl.ReadInt32());
                    break;
                case ParameterId.KeyHash:
                    // EndpointGuid と冗長なため明示的に消費 (skip)
                    break;
                case ParameterId.UnicastLocator:
                    {
                        var raw = pl.CurrentValueRaw();
                        if (raw.Length >= Locator.Size)
                        {
                            data.UnicastLocators.Add(Locator.Read(raw[..Locator.Size], littleEndian));
                        }
                        break;
                    }
                case ParameterId.MulticastLocator:
                    {
                        var raw = pl.CurrentValueRaw();
                        if (raw.Length >= Locator.Size)
                        {
                            data.MulticastLocators.Add(Locator.Read(raw[..Locator.Size], littleEndian));
                        }
                        break;
                    }
                default:
                    // 未知 PID は MoveNext が次へ進める際に自動スキップ
                    break;
            }
        }

        reader = pl.CurrentReader;
        return data;
    }
}
