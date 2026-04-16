using Rclsharp.Cdr;
using Rclsharp.Rtps.Writer;

using Guid = Rclsharp.Common.Guid;

namespace Rclsharp.Dds;

/// <summary>
/// 型付き Publisher。<see cref="ICdrSerializer{T}"/> でシリアライズして
/// <see cref="StatelessWriter"/> 経由で multicast に送信する。Best-Effort 専用 (Phase 5)。
/// </summary>
public sealed class Publisher<T> : IDisposable
{
    private readonly StatelessWriter _writer;
    private readonly ICdrSerializer<T> _serializer;
    private bool _disposed;

    public string TopicName { get; }
    public Guid Guid => _writer.Guid;

    public Publisher(string topicName, StatelessWriter writer, ICdrSerializer<T> serializer)
    {
        TopicName = topicName;
        _writer = writer;
        _serializer = serializer;
    }

    /// <summary>値をシリアライズ (encap header CDR_LE 付き) して 1 件送信する。</summary>
    public async ValueTask PublishAsync(T value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var payload = SerializeWithEncapsulation(value);
        await _writer.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>シリアライズ後のバイト列 (encap header 込み) を返す (テスト/デバッグ用)。</summary>
    public ReadOnlyMemory<byte> SerializeWithEncapsulation(T value)
    {
        // 概算サイズに余裕を加える (alignment/string padding 用)
        int sizeEstimate = _serializer.GetSerializedSize(value);
        int totalCapacity = CdrEncapsulation.Size + sizeEstimate + 16;
        var buffer = new byte[totalCapacity];
        CdrEncapsulation.Write(buffer, CdrEncapsulation.CdrLittleEndian);
        var w = new CdrWriter(buffer, CdrEndianness.LittleEndian, cdrOrigin: CdrEncapsulation.Size);
        _serializer.Serialize(ref w, in value);
        int payloadLength = w.Position;
        var payload = new byte[payloadLength];
        Buffer.BlockCopy(buffer, 0, payload, 0, payloadLength);
        return payload;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
