using System.Net;
using Rclsharp.Cdr;
using Rclsharp.Common;
using Rclsharp.Common.Logging;
using Rclsharp.Dds.QoS;
using Rclsharp.Discovery;
using Rclsharp.Rcl.Naming;
using Rclsharp.Rtps.Reader;
using Rclsharp.Rtps.Writer;
using Rclsharp.Transport;

using Guid = Rclsharp.Common.Guid;

namespace Rclsharp.Dds;

/// <summary>
/// rclsharp の Domain Participant。SPDP / SEDP / ユーザートピック transport を一元管理する。
/// Phase 6 で SEDP (Best-Effort) を追加。Reliable 化は Phase 7。
/// </summary>
public sealed class DomainParticipant : IDisposable
{
    private readonly DomainParticipantOptions _options;
    private readonly IRtpsTransport _multicastTransport;
    private readonly IRtpsTransport _unicastTransport;
    private readonly IRtpsTransport _userMulticastTransport;
    private readonly IRtpsTransport _userUnicastTransport;
    private readonly bool _ownsMulticastTransport;
    private readonly bool _ownsUnicastTransport;
    private readonly bool _ownsUserMulticastTransport;
    private readonly bool _ownsUserUnicastTransport;
    private readonly DiscoveryDb _discoveryDb;
    private readonly SpdpBuiltinParticipantReader _spdpReader;
    private readonly SpdpBuiltinParticipantWriter _spdpWriter;
    private readonly SedpEndpointWriter _sedpPublicationsWriter;
    private readonly SedpEndpointReader _sedpPublicationsReader;
    private readonly SedpEndpointWriter _sedpSubscriptionsWriter;
    private readonly SedpEndpointReader _sedpSubscriptionsReader;
    private readonly Locator _multicastDestination;
    private readonly Locator _userMulticastDestination;
    private readonly Locator _metatrafficUnicastLocator;
    private readonly Locator _metatrafficMulticastLocator;
    private readonly Locator _defaultMulticastLocator;
    private readonly Locator _defaultUnicastLocator;

    // ローカル endpoint 一覧 (SEDP 送信時に使用)
    private readonly object _localEndpointsLock = new();
    private readonly List<DiscoveredEndpointData> _localWriters = new();
    private readonly List<DiscoveredEndpointData> _localReaders = new();

    // ローカル StatelessWriter をトピック名でルックアップ (remote reader マッチング用)
    private readonly Dictionary<string, StatelessWriter> _localStatelessWriters = new();

    private bool _started;
    private bool _disposed;

    public DomainParticipantOptions Options => _options;
    public GuidPrefix GuidPrefix { get; }
    public Guid Guid { get; }
    public DiscoveryDb DiscoveryDb => _discoveryDb;

    /// <summary>ユーザートピックの multicast 送受信に使うトランスポート (Phase 5)。</summary>
    public IRtpsTransport UserMulticastTransport => _userMulticastTransport;

    /// <summary>ユーザートピックの unicast 送受信に使うトランスポート。</summary>
    public IRtpsTransport UserUnicastTransport => _userUnicastTransport;

    /// <summary>ユーザートピックの multicast 送信先 Locator (Phase 5)。</summary>
    public Locator UserMulticastDestination => _userMulticastDestination;

    public DomainParticipant(DomainParticipantOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        _options = options;

        GuidPrefix = GuidPrefix.CreateForCurrentProcess(_options.VendorId);
        Guid = new Guid(GuidPrefix, BuiltinEntityIds.Participant);

        int discoveryMulticastPort = RtpsPorts.DiscoveryMulticast(_options.DomainId);
        int discoveryUnicastPort = RtpsPorts.DiscoveryUnicast(_options.DomainId, _options.ParticipantId);
        int userMulticastPort = RtpsPorts.UserMulticast(_options.DomainId);
        int userUnicastPort = RtpsPorts.UserUnicast(_options.DomainId, _options.ParticipantId);

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

        // ユーザートピック用 Multicast transport (port = 7401 + 250*domain)
        if (_options.CustomUserMulticastTransport is not null)
        {
            _userMulticastTransport = _options.CustomUserMulticastTransport;
            _ownsUserMulticastTransport = false;
        }
        else
        {
            _userMulticastTransport = UdpTransport.CreateMulticast(
                _options.MulticastGroup,
                userMulticastPort,
                _options.MulticastInterface,
                _options.Logger);
            _ownsUserMulticastTransport = true;
        }

        // ユーザートピック用 Unicast transport (port = 7411 + 250*domain + 2*participant)
        if (_options.CustomUserUnicastTransport is not null)
        {
            _userUnicastTransport = _options.CustomUserUnicastTransport;
            _ownsUserUnicastTransport = false;
        }
        else
        {
            var localAddr = _options.LocalUnicastAddress ?? IPAddress.Loopback;
            _userUnicastTransport = UdpTransport.CreateUnicast(
                localAddr,
                userUnicastPort,
                _options.Logger);
            _ownsUserUnicastTransport = true;
        }

        _multicastDestination = Locator.FromUdpV4(_options.MulticastGroup, (uint)discoveryMulticastPort);
        _userMulticastDestination = Locator.FromUdpV4(_options.MulticastGroup, (uint)userMulticastPort);
        _metatrafficMulticastLocator = _multicastDestination;
        _metatrafficUnicastLocator = _unicastTransport.LocalLocator;
        _defaultMulticastLocator = _userMulticastDestination;
        _defaultUnicastLocator = _userUnicastTransport.LocalLocator;

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

        // SEDP (Phase 8 で Reliable 化)。
        // - Writer: multicast transport で送信、unicast transport で ACKNACK を受信
        // - Reader: multicast / unicast 両方で DATA/HB を受信、unicast で ACKNACK を返信
        _sedpPublicationsWriter = new SedpEndpointWriter(
            transport: _multicastTransport,
            multicastDestination: _multicastDestination,
            version: _options.ProtocolVersion,
            vendorId: _options.VendorId,
            localPrefix: GuidPrefix,
            writerEntityId: BuiltinEntityIds.SedpBuiltinPublicationsWriter,
            heartbeatPeriod: _options.SedpInterval,
            logger: _options.Logger);

        _sedpPublicationsReader = new SedpEndpointReader(
            replyTransport: _unicastTransport,
            discoveryDb: _discoveryDb,
            version: _options.ProtocolVersion,
            vendorId: _options.VendorId,
            localPrefix: GuidPrefix,
            readerEntityId: BuiltinEntityIds.SedpBuiltinPublicationsReader,
            ackNackFallbackDestination: _multicastDestination,
            producedEndpointKind: EndpointKind.Writer,
            logger: _options.Logger);

        _sedpSubscriptionsWriter = new SedpEndpointWriter(
            transport: _multicastTransport,
            multicastDestination: _multicastDestination,
            version: _options.ProtocolVersion,
            vendorId: _options.VendorId,
            localPrefix: GuidPrefix,
            writerEntityId: BuiltinEntityIds.SedpBuiltinSubscriptionsWriter,
            heartbeatPeriod: _options.SedpInterval,
            logger: _options.Logger);

        _sedpSubscriptionsReader = new SedpEndpointReader(
            replyTransport: _unicastTransport,
            discoveryDb: _discoveryDb,
            version: _options.ProtocolVersion,
            vendorId: _options.VendorId,
            localPrefix: GuidPrefix,
            readerEntityId: BuiltinEntityIds.SedpBuiltinSubscriptionsReader,
            ackNackFallbackDestination: _multicastDestination,
            producedEndpointKind: EndpointKind.Reader,
            logger: _options.Logger);

        // SPDP で remote participant を発見したら SEDP endpoint を auto-match
        _discoveryDb.ParticipantDiscovered += OnRemoteParticipantDiscovered;

        // SEDP で remote reader を発見したらローカル writer にユニキャストロケータを追加
        _discoveryDb.ReaderDiscovered += OnRemoteReaderDiscovered;
    }

    private void OnRemoteParticipantDiscovered(RemoteParticipant participant)
    {
        // remote SEDP endpoint の Guid を計算 (固定 EntityId)
        var prefix = participant.GuidPrefix;
        var remoteSedpPubReader = new Guid(prefix, BuiltinEntityIds.SedpBuiltinPublicationsReader);
        var remoteSedpPubWriter = new Guid(prefix, BuiltinEntityIds.SedpBuiltinPublicationsWriter);
        var remoteSedpSubReader = new Guid(prefix, BuiltinEntityIds.SedpBuiltinSubscriptionsReader);
        var remoteSedpSubWriter = new Guid(prefix, BuiltinEntityIds.SedpBuiltinSubscriptionsWriter);

        // remote の metatraffic unicast (ACKNACK 返送先 / DATA 送信先)
        Locator? remoteUnicast = participant.Data.MetatrafficUnicastLocators.Count > 0
            ? participant.Data.MetatrafficUnicastLocators[0]
            : null;

        // 自 writer ↔ remote reader
        _sedpPublicationsWriter.MatchRemoteReader(remoteSedpPubReader, remoteUnicast);
        _sedpSubscriptionsWriter.MatchRemoteReader(remoteSedpSubReader, remoteUnicast);

        // 自 reader ↔ remote writer (ACKNACK 返送先として remoteUnicast)
        _sedpPublicationsReader.MatchRemoteWriter(remoteSedpPubWriter, remoteUnicast);
        _sedpSubscriptionsReader.MatchRemoteWriter(remoteSedpSubWriter, remoteUnicast);

        _options.Logger.Debug($"DomainParticipant: auto-matched SEDP endpoints for {participant.Guid}");
    }

    private void OnRemoteReaderDiscovered(RemoteEndpoint remoteReader)
    {
        var topicName = remoteReader.TopicName;
        StatelessWriter? writer;
        lock (_localEndpointsLock)
        {
            _localStatelessWriters.TryGetValue(topicName, out writer);
        }
        if (writer is null) return;

        // remote reader のユニキャストロケータ (SEDP endpoint data から取得)
        // endpoint data にロケータがあればそれを使い、なければ participant の default unicast を使う
        var locators = remoteReader.Data.UnicastLocators;
        if (locators.Count > 0)
        {
            foreach (var loc in locators)
            {
                if (IsUdpLocator(loc)) writer.AddMatchedReaderLocator(loc);
            }
        }
        else
        {
            // endpoint にロケータがない場合は participant の DEFAULT_UNICAST_LOCATOR にフォールバック
            var participants = _discoveryDb.Snapshot();
            foreach (var p in participants)
            {
                if (p.GuidPrefix.Equals(remoteReader.Data.EndpointGuid.Prefix))
                {
                    foreach (var loc in p.Data.DefaultUnicastLocators)
                    {
                        if (IsUdpLocator(loc)) writer.AddMatchedReaderLocator(loc);
                    }
                    break;
                }
            }
        }

        _options.Logger.Debug($"DomainParticipant: matched local writer with remote reader on topic={topicName}");
    }

    private static bool IsUdpLocator(Locator loc)
        => loc.Kind == LocatorKind.UdpV4 || loc.Kind == LocatorKind.UdpV6;

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
        _userMulticastTransport.Start();
        _userUnicastTransport.Start();
        _spdpReader.Start();
        _spdpWriter.Start();

        // SEDP の DATA/HB は multicast (初期) と unicast (ACKNACK 返信先) の両方で受信する
        _multicastTransport.Received += _sedpPublicationsReader.OnPacketReceived;
        _multicastTransport.Received += _sedpSubscriptionsReader.OnPacketReceived;
        _unicastTransport.Received += _sedpPublicationsReader.OnPacketReceived;
        _unicastTransport.Received += _sedpSubscriptionsReader.OnPacketReceived;
        // SEDP writer は ACKNACK を unicast metatraffic で受ける
        _unicastTransport.Received += _sedpPublicationsWriter.OnPacketReceived;
        _unicastTransport.Received += _sedpSubscriptionsWriter.OnPacketReceived;

        _sedpPublicationsWriter.Start();
        _sedpSubscriptionsWriter.Start();
        _started = true;
    }

    /// <summary>SPDP / SEDP / Transport を停止する。</summary>
    public void Stop()
    {
        if (!_started)
        {
            return;
        }
        _sedpPublicationsWriter.Stop();
        _sedpSubscriptionsWriter.Stop();
        _multicastTransport.Received -= _sedpPublicationsReader.OnPacketReceived;
        _multicastTransport.Received -= _sedpSubscriptionsReader.OnPacketReceived;
        _unicastTransport.Received -= _sedpPublicationsReader.OnPacketReceived;
        _unicastTransport.Received -= _sedpSubscriptionsReader.OnPacketReceived;
        _unicastTransport.Received -= _sedpPublicationsWriter.OnPacketReceived;
        _unicastTransport.Received -= _sedpSubscriptionsWriter.OnPacketReceived;
        _spdpWriter.Stop();
        _spdpReader.Stop();
        _userUnicastTransport.Stop();
        _userMulticastTransport.Stop();
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
        data.DefaultUnicastLocators.Add(_defaultUnicastLocator);
        data.DefaultMulticastLocators.Add(_defaultMulticastLocator);
        return data;
    }

    /// <summary>
    /// 指定トピックの Publisher を生成する。
    /// EntityId は <see cref="UserEntityIdAllocator"/> によりトピック名から決定論的に割り当てる
    /// (Phase 5 互換)。同時にローカル endpoint 一覧へ登録され、SEDP で広告される (Phase 6)。
    /// </summary>
    public Publisher<T> CreatePublisher<T>(string topicName, ICdrSerializer<T> serializer, string? typeName = null)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(topicName)) throw new ArgumentException("Value cannot be null or empty.", nameof(topicName));
        if (serializer is null) throw new ArgumentNullException(nameof(serializer));

        var ddsTopic = TopicNameMangler.MangleTopic(topicName);
        var writerEntityId = UserEntityIdAllocator.WriterFor(ddsTopic);
        var writer = new StatelessWriter(
            _userMulticastTransport,
            _userMulticastDestination,
            _options.ProtocolVersion,
            _options.VendorId,
            GuidPrefix,
            writerEntityId,
            _options.Logger,
            unicastTransport: _userUnicastTransport);

        // SEDP 用に登録 (即時 publish)
        var endpointGuid = new Guid(GuidPrefix, writerEntityId);
        var endpointData = new DiscoveredEndpointData
        {
            Kind = EndpointKind.Writer,
            EndpointGuid = endpointGuid,
            ParticipantGuid = Guid,
            TopicName = ddsTopic,
            TypeName = typeName ?? "",
            Reliability = ReliabilityQos.BestEffort,
            Durability = DurabilityQos.Volatile,
        };
        endpointData.UnicastLocators.Add(_defaultUnicastLocator);
        endpointData.MulticastLocators.Add(_defaultMulticastLocator);
        lock (_localEndpointsLock)
        {
            _localWriters.Add(endpointData);
            _localStatelessWriters[ddsTopic] = writer;
        }
        _ = _sedpPublicationsWriter.AddEndpointAsync(endpointData);

        return new Publisher<T>(topicName, writer, serializer);
    }

    /// <summary>
    /// 指定トピックの Subscription を生成する。
    /// 受信ループは即座に開始され、マッチする DATA を受信するとハンドラが呼ばれる。
    /// 同時にローカル endpoint 一覧へ登録され、SEDP で広告される (Phase 6)。
    /// </summary>
    public Subscription<T> CreateSubscription<T>(
        string topicName,
        ICdrSerializer<T> serializer,
        Action<T, GuidPrefix> handler,
        string? typeName = null)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(topicName)) throw new ArgumentException("Value cannot be null or empty.", nameof(topicName));
        if (serializer is null) throw new ArgumentNullException(nameof(serializer));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        var ddsTopic = TopicNameMangler.MangleTopic(topicName);
        var writerEntityId = UserEntityIdAllocator.WriterFor(ddsTopic);
        var readerEntityId = UserEntityIdAllocator.ReaderFor(ddsTopic);
        var reader = new StatelessReader(_userMulticastTransport, writerEntityId, _options.Logger);

        // SEDP 用に登録 (即時 publish)
        var endpointGuid = new Guid(GuidPrefix, readerEntityId);
        var endpointData = new DiscoveredEndpointData
        {
            Kind = EndpointKind.Reader,
            EndpointGuid = endpointGuid,
            ParticipantGuid = Guid,
            TopicName = ddsTopic,
            TypeName = typeName ?? "",
            Reliability = ReliabilityQos.BestEffort,
            Durability = DurabilityQos.Volatile,
        };
        endpointData.UnicastLocators.Add(_defaultUnicastLocator);
        endpointData.MulticastLocators.Add(_defaultMulticastLocator);
        lock (_localEndpointsLock) { _localReaders.Add(endpointData); }
        _ = _sedpSubscriptionsWriter.AddEndpointAsync(endpointData);

        return new Subscription<T>(topicName, reader, serializer, handler);
    }

    /// <summary>ハンドラが GuidPrefix を必要としない場合のショートカット。</summary>
    public Subscription<T> CreateSubscription<T>(
        string topicName,
        ICdrSerializer<T> serializer,
        Action<T> handler)
        => CreateSubscription<T>(topicName, serializer, (value, _) => handler(value));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
        _sedpPublicationsWriter.Dispose();
        _sedpSubscriptionsWriter.Dispose();
        _sedpPublicationsReader.Dispose();
        _sedpSubscriptionsReader.Dispose();
        _spdpWriter.Dispose();
        _spdpReader.Dispose();
        if (_ownsUserUnicastTransport)
        {
            _userUnicastTransport.Dispose();
        }
        if (_ownsUserMulticastTransport)
        {
            _userMulticastTransport.Dispose();
        }
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
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
    }
}
