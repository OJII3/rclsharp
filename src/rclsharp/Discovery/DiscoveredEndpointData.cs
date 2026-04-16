using Rclsharp.Common;
using Rclsharp.Dds.QoS;

using Guid = Rclsharp.Common.Guid;

namespace Rclsharp.Discovery;

/// <summary>
/// SEDP で交換される endpoint (Writer または Reader) の発見データ。
/// RTPS 仕様 9.6.2.2.4 / 9.6.2.2.5 (DiscoveredWriterData / DiscoveredReaderData)。
/// 共通フィールドのみを保持し、Writer/Reader の区別は <see cref="Kind"/> で表現する。
///
/// <para>
/// Phase 6 では最低限の PID のみ:
/// PARTICIPANT_GUID / ENDPOINT_GUID / TOPIC_NAME / TYPE_NAME / RELIABILITY / DURABILITY。
/// QoS 拡張 (Deadline, Lifespan, ...) は後続フェーズで追加。
/// </para>
/// </summary>
public sealed class DiscoveredEndpointData
{
    public EndpointKind Kind { get; set; }

    /// <summary>endpoint の Guid (PID_ENDPOINT_GUID)。必須。</summary>
    public Guid EndpointGuid { get; set; }

    /// <summary>endpoint を所有する Participant の Guid (PID_PARTICIPANT_GUID)。必須。</summary>
    public Guid ParticipantGuid { get; set; }

    /// <summary>マングル済みトピック名 (PID_TOPIC_NAME)、例 "rt/chatter"。必須。</summary>
    public string TopicName { get; set; } = "";

    /// <summary>マングル済み型名 (PID_TYPE_NAME)、例 "std_msgs::msg::dds_::String_"。必須。</summary>
    public string TypeName { get; set; } = "";

    public ReliabilityQos Reliability { get; set; } = ReliabilityQos.BestEffort;
    public DurabilityQos Durability { get; set; } = DurabilityQos.Volatile;
}
