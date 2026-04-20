using Rclsharp.Cdr;
using Rclsharp.Common;
using Rclsharp.Common.Logging;
using Rclsharp.Rtps.Submessages;
using Rclsharp.Transport;

using Guid = Rclsharp.Common.Guid;

namespace Rclsharp.Rtps.Writer;

/// <summary>
/// Stateless RTPS Writer。Best-Effort QoS 用。
/// マルチキャスト送信に加え、SEDP でマッチした remote reader のユニキャストロケータにも送信する。
///
/// <para>
/// 各 <see cref="WriteAsync"/> 呼び出しで writerSN を 1 つインクリメントし、
/// INFO_TS + DATA を含む RTPS message を送信する。
/// </para>
/// </summary>
public sealed class StatelessWriter
{
    /// <summary>送信バッファサイズ (1500 = MTU 想定)。Phase 5 ではフラグメンテーション非対応。</summary>
    public const int SendBufferSize = 1500;

    private readonly IRtpsTransport _transport;
    private readonly IRtpsTransport? _unicastTransport;
    private readonly Locator _multicastDestination;
    private readonly ProtocolVersion _version;
    private readonly VendorId _vendorId;
    private readonly GuidPrefix _localPrefix;
    private readonly EntityId _writerEntityId;
    private readonly ILogger _logger;

    private readonly object _matchedReadersLock = new();
    private readonly List<Locator> _matchedReaderLocators = new();

    private long _sequenceNumber;

    public Guid Guid { get; }
    public EntityId WriterEntityId => _writerEntityId;

    public StatelessWriter(
        IRtpsTransport transport,
        Locator multicastDestination,
        ProtocolVersion version,
        VendorId vendorId,
        GuidPrefix localPrefix,
        EntityId writerEntityId,
        ILogger? logger = null,
        IRtpsTransport? unicastTransport = null)
    {
        _transport = transport;
        _multicastDestination = multicastDestination;
        _version = version;
        _vendorId = vendorId;
        _localPrefix = localPrefix;
        _writerEntityId = writerEntityId;
        _logger = logger ?? NullLogger.Instance;
        _unicastTransport = unicastTransport;
        Guid = new Guid(localPrefix, writerEntityId);
    }

    /// <summary>マッチした remote reader のユニキャストロケータを追加する。</summary>
    public void AddMatchedReaderLocator(Locator locator)
    {
        lock (_matchedReadersLock)
        {
            if (!_matchedReaderLocators.Contains(locator))
            {
                _matchedReaderLocators.Add(locator);
                _logger.Debug($"StatelessWriter {_writerEntityId}: added matched reader locator {locator}");
            }
        }
    }

    /// <summary>シリアライズ済みペイロード (encap ヘッダ含む) を送信する。</summary>
    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> serializedPayload,
        CancellationToken cancellationToken = default)
    {
        long sn = Interlocked.Increment(ref _sequenceNumber);
        var packet = BuildPacket(serializedPayload, sn);

        // マルチキャスト送信
        try
        {
            await _transport.SendAsync(packet, _multicastDestination, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.Error("StatelessWriter multicast SendAsync failed", ex);
        }

        // マッチした reader のユニキャストロケータにも送信
        Locator[] locators;
        lock (_matchedReadersLock)
        {
            if (_matchedReaderLocators.Count == 0) return;
            locators = _matchedReaderLocators.ToArray();
        }

        var unicastTransport = _unicastTransport ?? _transport;
        foreach (var locator in locators)
        {
            try
            {
                await unicastTransport.SendAsync(packet, locator, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.Error($"StatelessWriter unicast SendAsync to {locator} failed", ex);
            }
        }
    }

    /// <summary>RTPS message (header + INFO_TS + DATA) を組み立てる (テスト用に public)。</summary>
    public ReadOnlyMemory<byte> BuildPacket(ReadOnlyMemory<byte> serializedPayload, long sequenceNumber)
    {
        var buffer = new byte[SendBufferSize];
        var writer = new RtpsMessageWriter(buffer, _version, _vendorId, _localPrefix);
        writer.WriteInfoTimestamp(new InfoTimestampSubmessage(Time.Now()));
        var dataSubmsg = new DataSubmessage(
            readerEntityId: EntityId.Unknown,
            writerEntityId: _writerEntityId,
            writerSn: new SequenceNumber(sequenceNumber),
            serializedPayload: serializedPayload,
            dataPresent: true);
        writer.WriteData(dataSubmsg, CdrEndianness.LittleEndian);

        var result = new byte[writer.BytesWritten];
        writer.WrittenSpan.CopyTo(result);
        return result;
    }
}
