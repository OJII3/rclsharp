namespace Rclsharp.Cdr;

/// <summary>
/// OMG CDR (Common Data Representation) リーダ。ReadOnlySpan&lt;byte&gt; から直接読み出す。
/// 本実装は Phase 1 で行う。Phase 0 では <see cref="ICdrSerializer{T}"/> の API を確定させるための前方宣言。
/// </summary>
public ref struct CdrReader
{
    // Phase 1 で実装: ReadOnlySpan<byte> _buffer, int _position, Endianness, AlignTo, ReadByte/Int32/String/...
}
