using Rclsharp.Common;

namespace Rclsharp.Rcl.Naming;

/// <summary>
/// ユーザートピック用の EntityId を、トピック名から決定論的に割り当てる。
/// SEDP 未実装の現状では、Pub/Sub の matching を「同じトピック名 → 同じ writer/reader EntityId」で行うために必要。
/// 24bit entityKey は FNV-1a ハッシュで生成、kind は WriterNoKey (0x03) / ReaderNoKey (0x04)。
///
/// <para>
/// SEDP 実装後 (Phase 6) は実 GUID で matching するため不要になる予定。
/// </para>
/// </summary>
public static class UserEntityIdAllocator
{
    /// <summary>topic 名から WriterNoKey EntityId を計算する。</summary>
    public static EntityId WriterFor(string topicName)
        => Allocate(topicName, EntityKind.UserDefinedWriterNoKey);

    /// <summary>topic 名から ReaderNoKey EntityId を計算する。</summary>
    public static EntityId ReaderFor(string topicName)
        => Allocate(topicName, EntityKind.UserDefinedReaderNoKey);

    private static EntityId Allocate(string topicName, EntityKind kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(topicName);
        uint key = Fnv1a24(topicName);
        return new EntityId(key, kind);
    }

    /// <summary>FNV-1a を 24bit にトリミングしたハッシュ。プロセス間/起動間で一致する。</summary>
    private static uint Fnv1a24(string value)
    {
        const uint offsetBasis = 2166136261u;
        const uint prime = 16777619u;
        uint hash = offsetBasis;
        for (int i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }
        return hash & 0x00FF_FFFFu;
    }
}
