using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Rclsharp.Cdr.ParameterList;

/// <summary>
/// PL_CDR ParameterList ライタ。RTPS 仕様 9.4.2.11。
/// 各パラメータは PID(uint16) + length(uint16) + value (4B 整列) で構成される。
/// 末尾には <see cref="ParameterId.Sentinel"/> (length=0) を書く必要がある。
/// </summary>
/// <remarks>
/// パラメータ値の内部 alignment は外側の <see cref="CdrWriter"/> の <c>cdrOrigin</c> を継承する。
/// PL_CDR で使われる主要な値型 (uint8/16/32, Time_t/Duration_t (= 4B+4B), Locator, Guid, ProtocolVersion, VendorId)
/// は最大 4B 整列で十分なため、Phase 1 ではこの単純化で問題ない。
/// </remarks>
public ref struct ParameterListWriter
{
    private CdrWriter _writer;
    private int _currentLengthPos;   // -1 if no open parameter
    private int _currentValueStart;

    public CdrEndianness Endianness => _writer.Endianness;

    [UnscopedRef]
    public ref CdrWriter Inner => ref _writer;

    public ParameterListWriter(CdrWriter writer)
    {
        _writer = writer;
        _currentLengthPos = -1;
        _currentValueStart = -1;
    }

    /// <summary>
    /// 新規パラメータを開始する。PID と length プレースホルダ (0) を書き、
    /// 呼び出し側は <see cref="Inner"/> 経由で値を書く。書き終えたら <see cref="EndParameter"/>。
    /// </summary>
    public void BeginParameter(ushort pid)
    {
        if (_currentLengthPos >= 0)
        {
            throw new InvalidOperationException("Previous parameter is not ended.");
        }
        _writer.AlignTo(4);
        _writer.WriteUInt16(pid);
        _currentLengthPos = _writer.Position;
        _writer.WriteUInt16(0); // length placeholder
        _currentValueStart = _writer.Position;
    }

    /// <summary>
    /// 現在のパラメータを閉じる。値を 4B 境界にパディングし、length フィールドへ書き戻す。
    /// </summary>
    public void EndParameter()
    {
        if (_currentLengthPos < 0)
        {
            throw new InvalidOperationException("No parameter is open.");
        }
        int valueLen = _writer.Position - _currentValueStart;
        int paddedLen = (valueLen + 3) & ~3;
        // Pad to 4 with zeros
        for (int i = valueLen; i < paddedLen; i++)
        {
            _writer.WriteByte(0);
        }
        if (paddedLen > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"Parameter value length {paddedLen} exceeds uint16 maximum.");
        }
        // Patch length field
        Span<byte> lenSlot = _writer.RawBuffer.Slice(_currentLengthPos, 2);
        if (_writer.Endianness == CdrEndianness.LittleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(lenSlot, (ushort)paddedLen);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(lenSlot, (ushort)paddedLen);
        }
        _currentLengthPos = -1;
        _currentValueStart = -1;
    }

    /// <summary>
    /// SENTINEL マーカーを書く。ParameterList の終端必須。
    /// </summary>
    public void WriteSentinel()
    {
        if (_currentLengthPos >= 0)
        {
            throw new InvalidOperationException("Previous parameter is not ended.");
        }
        _writer.AlignTo(4);
        _writer.WriteUInt16(ParameterId.Sentinel);
        _writer.WriteUInt16(0);
    }
}
