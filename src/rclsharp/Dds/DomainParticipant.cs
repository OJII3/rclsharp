using System.Net;
using Rclsharp.Common;
using Rclsharp.Discovery;
using Rclsharp.Transport;

using Guid = Rclsharp.Common.Guid;

namespace Rclsharp.Dds;

/// <summary>
/// rclsharp の Domain Participant。SPDP の起動・停止と Discovery 状態の管理を行う。
/// Phase 4 ではメタトラフィック (SPDP) のみ。SEDP / ユーザートピックは後続フェーズ。
/// </summary>
public sealed class DomainParticipant : IDisposable
{
    private readonly DomainParticipantOptions _options;
    private readonly IRtpsTransport _multicastTransport;
    private readonly IRtpsTransport _unicastTransport;
    private readonly bool _ownsMulticastTransport;
    private readonly bool _ownsUnicastTransport;
    private readonly DiscoveryDb _discoveryDb;
    private readonly SpdpBuiltinParticipantReader _spdpReader;
    private readonly SpdpBuiltinParticipantWriter _spdpWriter;
    private readonly Locator _multicastDestination;
    private readonly Locator _metatrafficUnicastLocator;
    private readonly Locator _metatrafficMulticastLocator;

    private bool _started;
    private bool _disposed;

    public DomainParticipantOptions Options => _options;
    public GuidPrefix GuidPrefix { get; }
    public Guid Guid { get; }
    public DiscoveryDb DiscoveryDb => _discoveryDb;

    public DomainParticipant(DomainParticipantOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        GuidPrefix = GuidPrefix.CreateForCurrentProcess(_options.VendorId);
        Guid = new Guid(GuidPrefix, BuiltinEntityIds.Participant);

        int discoveryMulticastPort = RtpsPorts.DiscoveryMulticast(_options.DomainId);
        int discoveryUnicastPort = RtpsPorts.DiscoveryUnicast(_options.DomainId, _options.ParticipantId);

        // Multicast transport (受信用に bind、送信先 Locator も同じ)
        if (_options.CustomMulticastTransport is not null)
        {
            _multicastTransport = _options.CustomMulticastTransport;
            _ownsMulticastTransport = false;
        }
        else
        {
            _multicastTransport = UdpTransport.CreateMulticast(
                _options.MulticastGroup,
                discoveryMulticastPort,
                _options.MulticastInterface,
                _options.Logger);
            _ownsMulticastTransport = true;
        }

        // Unicast transport (Metatraffic 受信用)
        if (_options.CustomUnicastTransport is not null)
        {
            _unicastTransport = _options.CustomUnicastTransport;
            _ownsUnicastTransport = false;
        }
        else
        {
            var localAddr = _options.LocalUnicastAddress ?? IPAddress.Loopback;
            _unicastTransport = UdpTransport.CreateUnicast(
                localAddr,
                discoveryUnicastPort,
                _options.Logger);
            _ownsUnicastTransport = true;
        }

        _multicastDestination = Locator.FromUdpV4(_options.MulticastGroup, (uint)discoveryMulticastPort);
        _metatrafficMulticastLocator = _multicastDestination;
        _metatrafficUnicastLocator = _unicastTransport.LocalLocator;

        _discoveryDb = new DiscoveryDb();

        _spdpReader = new SpdpBuiltinParticipantReader(
            _multicastTransport, _discoveryDb, GuidPrefix, _options.Logger);

        _spdpWriter = new SpdpBuiltinParticipantWriter(
            transport: _multicastTransport,
            multicastDestination: _multicastDestination,
            version: _options.ProtocolVersion,
            vendorId: _options.VendorId,
            localPrefix: GuidPrefix,
            participantDataProvider: BuildParticipantData,
            interval: _options.SpdpInterval,
            logger: _options.Logger);
    }

    /// <summary>送受信トランスポートと SPDP の起動。</summary>
    public void Start()
    {
        ThrowIfDisposed();
        if (_started)
        {
            return;
        }
        _multicastTransport.Start();
        _unicastTransport.Start();
        _spdpReader.Start();
        _spdpWriter.Start();
        _started = true;
    }

    /// <summary>SPDP / Transport を停止する。</summary>
    public void Stop()
    {
        if (!_started)
        {
            return;
        }
        _spdpWriter.Stop();
        _spdpReader.Stop();
        _multicastTransport.Stop();
        _unicastTransport.Stop();
        _started = false;
    }

    /// <summary>現在の自 Participant の <see cref="ParticipantData"/> を生成する (SPDP 送信時に使われる)。</summary>
    public ParticipantData BuildParticipantData()
    {
        var data = new ParticipantData
        {
            ProtocolVersion = _options.ProtocolVersion,
            VendorId = _options.VendorId,
            Guid = Guid,
            BuiltinEndpoints = BuiltinEndpointSet.RclsharpDefault,
            LeaseDuration = _options.LeaseDuration,
            EntityName = _options.EntityName,
        };
        data.MetatrafficMulticastLocators.Add(_metatrafficMulticastLocator);
        data.MetatrafficUnicastLocators.Add(_metatrafficUnicastLocator);
        return data;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
        _spdpWriter.Dispose();
        _spdpReader.Dispose();
        if (_ownsMulticastTransport)
        {
            _multicastTransport.Dispose();
        }
        if (_ownsUnicastTransport)
        {
            _unicastTransport.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
