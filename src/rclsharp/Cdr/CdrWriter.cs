namespace Rclsharp.Cdr;

/// <summary>
/// OMG CDR (Common Data Representation) ライタ。Span&lt;byte&gt; 上に直接書き込む。
/// 本実装は Phase 1 で行う。Phase 0 では <see cref="ICdrSerializer{T}"/> の API を確定させるための前方宣言。
/// </summary>
public ref struct CdrWriter
{
    // Phase 1 で実装: Span<byte> _buffer, int _position, Endianness, AlignTo, WriteByte/Int32/String/...
}
