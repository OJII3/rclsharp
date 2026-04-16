using System.Diagnostics.CodeAnalysis;

namespace Rclsharp.Cdr.ParameterList;

/// <summary>
/// PL_CDR ParameterList リーダ。RTPS 仕様 9.4.2.11。
/// <see cref="MoveNext"/> でパラメータを順次走査する。値を読まなかった場合、
/// 次の <see cref="MoveNext"/> で残りバイトが自動スキップされる。
/// </summary>
public ref struct ParameterListReader
{
    private CdrReader _reader;
    private ushort _currentLength;
    private int _currentValueStart;

    public CdrEndianness Endianness => _reader.Endianness;

    [UnscopedRef]
    public ref CdrReader Inner => ref _reader;

    public ParameterListReader(CdrReader reader)
    {
        _reader = reader;
        _currentLength = 0;
        _currentValueStart = -1;
    }

    /// <summary>
    /// 次のパラメータ先頭に進む。SENTINEL に達した場合は false を返す。
    /// 直前の値を <see cref="Inner"/> から完全に読み出していなくても、ここで自動スキップされる。
    /// </summary>
    public bool MoveNext(out ushort pid, out ushort length)
    {
        // 直前のパラメータの未読部分をスキップ
        if (_currentValueStart >= 0)
        {
            int consumed = _reader.Position - _currentValueStart;
            int remaining = _currentLength - consumed;
            if (remaining > 0)
            {
                _reader.Skip(remaining);
            }
            _currentValueStart = -1;
        }
        _reader.AlignTo(4);
        pid = _reader.ReadUInt16();
        length = _reader.ReadUInt16();
        if (pid == ParameterId.Sentinel)
        {
            return false;
        }
        _currentLength = length;
        _currentValueStart = _reader.Position;
        return true;
    }

    /// <summary>現在のパラメータの値部分の長さ (バイト)。</summary>
    public int CurrentValueLength => _currentLength;

    /// <summary>現在のパラメータ値領域 (生バイト) を取得する。<see cref="Inner"/> を直接読まずに済むショートカット。</summary>
    [UnscopedRef]
    public ReadOnlySpan<byte> CurrentValueRaw()
    {
        if (_currentValueStart < 0)
        {
            throw new InvalidOperationException("No parameter is being read.");
        }
        return _reader.RawBuffer.Slice(_currentValueStart, _currentLength);
    }
}
