using System.Buffers;
using Rclsharp.Cdr;
using Rclsharp.Common;
using Rclsharp.Common.Logging;
using Rclsharp.Rtps;
using Rclsharp.Rtps.Submessages;
using Rclsharp.Transport;

namespace Rclsharp.Discovery;

/// <summary>
/// SEDP の Publications または Subscriptions Writer。
/// 周期的にローカル endpoint 一覧を <see cref="DiscoveredEndpointData"/> として PL_CDR シリアライズし、
/// SEDP_BUILTIN_*_WRITER の EntityId を持つ DATA submessage で送信する。
///
/// <para>
/// Phase 6 では Best-Effort (StatelessWriter 相当)。Reliable 化は Phase 7 で。
/// </para>
/// </summary>
public sealed class SedpEndpointWriter : IDisposable
{
    public const int SendBufferSize = 1500;

    private readonly IRtpsTransport _transport;
    private readonly Locator _destination;
    private readonly ProtocolVersion _version;
    private readonly VendorId _vendorId;
    private readonly GuidPrefix _localPrefix;
    private readonly EntityId _writerEntityId;
    private readonly Func<IReadOnlyList<DiscoveredEndpointData>> _endpointsProvider;
    private readonly TimeSpan _interval;
    private readonly ILogger _logger;

    private long _sequenceNumber;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _disposed;

    public SedpEndpointWriter(
        IRtpsTransport transport,
        Locator destination,
        ProtocolVersion version,
        VendorId vendorId,
        GuidPrefix localPrefix,
        EntityId writerEntityId,
        Func<IReadOnlyList<DiscoveredEndpointData>> endpointsProvider,
        TimeSpan interval,
        ILogger? logger = null)
    {
        _transport = transport;
        _destination = destination;
        _version = version;
        _vendorId = vendorId;
        _localPrefix = localPrefix;
        _writerEntityId = writerEntityId;
        _endpointsProvider = endpointsProvider;
        _interval = interval;
        _logger = logger ?? NullLogger.Instance;
    }

    public void Start()
    {
        ThrowIfDisposed();
        if (_loopTask is not null)
        {
            return;
        }
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loopTask = Task.Run(() => SendLoopAsync(token), token);
    }

    public void Stop()
    {
        if (_cts is null)
        {
            return;
        }
        _cts.Cancel();
        try { _loopTask?.Wait(TimeSpan.FromSeconds(1)); }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException)) { }
        catch (Exception ex) { _logger.Warn("SedpEndpointWriter loop did not exit cleanly", ex); }
        _cts.Dispose();
        _cts = null;
        _loopTask = null;
    }

    /// <summary>現在のローカル endpoint 一覧を 1 サイクル送信する。</summary>
    public async Task SendAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var endpoints = _endpointsProvider();
        foreach (var endpoint in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long sn = Interlocked.Increment(ref _sequenceNumber);
            var packet = BuildPacket(endpoint, sn);
            try
            {
                await _transport.SendAsync(packet, _destination, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.Error("SedpEndpointWriter send failed", ex);
            }
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        await SendAllAsync(cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await Task.Delay(_interval, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            await SendAllAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>1 endpoint 分の RTPS message を組み立てる (テスト用に public)。</summary>
    public ReadOnlyMemory<byte> BuildPacket(DiscoveredEndpointData endpoint, long sequenceNumber)
    {
        // SerializedPayload (encap PL_CDR_LE 4B + PL_CDR)
        var payloadBuf = ArrayPool<byte>.Shared.Rent(1024);
        int payloadLength;
        try
        {
            CdrEncapsulation.Write(payloadBuf, CdrEncapsulation.PlCdrLittleEndian);
            var inner = new CdrWriter(payloadBuf, CdrEndianness.LittleEndian, cdrOrigin: CdrEncapsulation.Size);
            DiscoveredEndpointDataSerializer.Write(ref inner, endpoint);
            payloadLength = inner.Position;
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(payloadBuf);
            throw;
        }

        var payload = new byte[payloadLength];
        Buffer.BlockCopy(payloadBuf, 0, payload, 0, payloadLength);
        ArrayPool<byte>.Shared.Return(payloadBuf);

        // RTPS message (header + INFO_TS + DATA)
        var messageBuf = new byte[SendBufferSize];
        var writer = new RtpsMessageWriter(messageBuf, _version, _vendorId, _localPrefix);
        writer.WriteInfoTimestamp(new InfoTimestampSubmessage(Time.Now()));
        var dataSubmsg = new DataSubmessage(
            readerEntityId: EntityId.Unknown,
            writerEntityId: _writerEntityId,
            writerSn: new SequenceNumber(sequenceNumber),
            serializedPayload: payload,
            dataPresent: true);
        writer.WriteData(dataSubmsg);

        var result = new byte[writer.BytesWritten];
        writer.WrittenSpan.CopyTo(result);
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
