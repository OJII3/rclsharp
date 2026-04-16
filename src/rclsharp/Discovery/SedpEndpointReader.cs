using Rclsharp.Cdr;
using Rclsharp.Common;
using Rclsharp.Common.Logging;
using Rclsharp.Rtps;
using Rclsharp.Rtps.Submessages;
using Rclsharp.Transport;

namespace Rclsharp.Discovery;

/// <summary>
/// SEDP の Publications または Subscriptions Reader。
/// transport の Received を購読し、SEDP_BUILTIN_*_WRITER から送信された DATA を解釈して
/// <see cref="DiscoveryDb.UpsertEndpoint"/> へ流す。
/// </summary>
public sealed class SedpEndpointReader : IDisposable
{
    private readonly IRtpsTransport _transport;
    private readonly DiscoveryDb _discoveryDb;
    private readonly GuidPrefix _localPrefix;
    private readonly EntityId _expectedWriterEntityId;
    private readonly EndpointKind _producedEndpointKind;
    private readonly ILogger _logger;
    private readonly Func<DateTime> _clock;

    private bool _started;
    private bool _disposed;

    /// <summary>SEDP DATA を受信して解析できたとき発火 (DiscoveryDb 更新前)。</summary>
    public event Action<DiscoveredEndpointData>? EndpointDataReceived;

    public SedpEndpointReader(
        IRtpsTransport transport,
        DiscoveryDb discoveryDb,
        GuidPrefix localPrefix,
        EntityId expectedWriterEntityId,
        EndpointKind producedEndpointKind,
        ILogger? logger = null,
        Func<DateTime>? clock = null)
    {
        _transport = transport;
        _discoveryDb = discoveryDb;
        _localPrefix = localPrefix;
        _expectedWriterEntityId = expectedWriterEntityId;
        _producedEndpointKind = producedEndpointKind;
        _logger = logger ?? NullLogger.Instance;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public void Start()
    {
        ThrowIfDisposed();
        if (_started) return;
        _transport.Received += OnPacketReceived;
        _started = true;
    }

    public void Stop()
    {
        if (!_started) return;
        _transport.Received -= OnPacketReceived;
        _started = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void OnPacketReceived(ReadOnlyMemory<byte> packet, Locator source)
    {
        try { ProcessPacket(packet.Span); }
        catch (Exception ex) { _logger.Warn($"SedpEndpointReader failed to parse packet from {source}", ex); }
    }

    public void ProcessPacket(ReadOnlySpan<byte> packet)
    {
        if (!RtpsHeader.TryRead(packet, out _, out _, out _))
        {
            return;
        }
        var reader = new RtpsMessageReader(packet);
        while (reader.TryReadNext(out var hdr, out var body))
        {
            if (hdr.Kind != SubmessageKind.Data) continue;
            var data = DataSubmessage.ReadBody(body, hdr.Endianness, hdr.Flags);
            if (!data.WriterEntityId.Equals(_expectedWriterEntityId)) continue;
            if (data.SerializedPayload.IsEmpty) continue;
            HandleSedpData(data.SerializedPayload.Span);
        }
    }

    private void HandleSedpData(ReadOnlySpan<byte> serializedPayload)
    {
        if (serializedPayload.Length < CdrEncapsulation.Size)
        {
            _logger.Debug("SedpEndpointReader: payload too small for encap header");
            return;
        }
        var (kind, _) = CdrEncapsulation.Read(serializedPayload[..CdrEncapsulation.Size]);
        if (!CdrEncapsulation.IsParameterList(kind))
        {
            _logger.Debug($"SedpEndpointReader: unexpected encapsulation 0x{kind:X4}");
            return;
        }
        var endian = CdrEncapsulation.GetEndianness(kind);
        var cdrReader = new CdrReader(serializedPayload, endian, cdrOrigin: CdrEncapsulation.Size);
        var endpointData = DiscoveredEndpointDataSerializer.Read(ref cdrReader, _producedEndpointKind);

        EndpointDataReceived?.Invoke(endpointData);
        _discoveryDb.UpsertEndpoint(endpointData, _clock(), ignorePrefix: _localPrefix);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
