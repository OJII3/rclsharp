using System.Runtime.CompilerServices;

namespace Rclsharp.Common;

/// <summary>
/// RTPS GuidPrefix (12 バイト固定)。RTPS 仕様 8.2.4.3。
/// 通常は先頭 2 バイトに VendorId、残りに hostId / processId / counter が入る。
/// 詳細レイアウトはベンダ依存。rclsharp の生成方針は Phase 4 で確定する。
/// </summary>
public struct GuidPrefix : IEquatable<GuidPrefix>
{
    public const int Size = 12;

    private GuidPrefixStorage _storage;

    public GuidPrefix(ReadOnlySpan<byte> source)
    {
        if (source.Length != Size)
        {
            throw new ArgumentException(
                $"GuidPrefix requires exactly {Size} bytes, got {source.Length}.", nameof(source));
        }
        _storage = default;
        source.CopyTo(_storage);
    }

    /// <summary>すべて 0 の Unknown GuidPrefix。</summary>
    public static GuidPrefix Unknown => default;

    public byte this[int index] => _storage[index];

    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException(
                $"Destination requires at least {Size} bytes.", nameof(destination));
        }
        ReadOnlySpan<byte> source = _storage;
        source.CopyTo(destination);
    }

    public byte[] ToByteArray()
    {
        var arr = new byte[Size];
        CopyTo(arr);
        return arr;
    }

    public bool Equals(GuidPrefix other)
    {
        ReadOnlySpan<byte> a = _storage;
        ReadOnlySpan<byte> b = other._storage;
        return a.SequenceEqual(b);
    }

    public override bool Equals(object? obj) => obj is GuidPrefix gp && Equals(gp);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        ReadOnlySpan<byte> bytes = _storage;
        hash.AddBytes(bytes);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        ReadOnlySpan<byte> bytes = _storage;
        return Convert.ToHexString(bytes);
    }

    public static bool operator ==(GuidPrefix left, GuidPrefix right) => left.Equals(right);
    public static bool operator !=(GuidPrefix left, GuidPrefix right) => !left.Equals(right);
}

/// <summary>
/// GuidPrefix の 12 バイト固定ストレージ。InlineArray により値型として確保される。
/// </summary>
[InlineArray(GuidPrefix.Size)]
internal struct GuidPrefixStorage
{
    private byte _element0;
}
