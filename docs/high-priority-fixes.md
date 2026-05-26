# 高優先修正方針

この文書は、通信不良に直結する監査結果を修正するための短期方針をまとめる。
対象は既存の DDSI-RTPS wire compatibility を壊しやすい箇所に限定し、後方互換用の代替経路は追加しない。

## 優先対象

1. RTPS message/submessage の解釈を正す。
   - `INFO_TS` invalidate や `PAD` の length=0 を、常に message 末尾までとは扱わない。
   - reliable reader が `GAP` を処理し、欠損済み sequence number を要求し続けないようにする。
   - 送信側 writer が MTU 超過 payload を `DATA_FRAG` として送れるようにする。

2. 実 UDP の lifecycle と port 境界を正す。
   - `Stop()` 後に同じ transport を `Start()` できるようにする。
   - RTPS port 計算は domain/participant の組み合わせで 65535 以下を保証する。

3. Discovery / matching の stale state と誤 match を減らす。
   - participant lease 失効時に remote endpoint と local matching を削除する。
   - 既存 proxy の locator update を反映する。
   - 空 type name を wildcard として扱わない。

4. CDR/ParameterList の誤解釈を減らす。
   - vendor-specific PID を標準 PID として解釈しない。
   - unknown must-understand PID は silent skip しない。
   - unsupported encapsulation kind は明示的に拒否する。

5. Unity lifecycle と長時間実行で残る background 処理を止める。
   - core は Unity 依存を持たないため、UniTask へ全面置換しない。
   - core の非同期処理は `Task` / `ValueTask` と `CancellationToken` で停止可能にし、
     Unity 側は `SynchronizationContext` や dispatcher adapter からメインスレッドへ配送する。
   - `DomainParticipant.Stop()` は participant が作成した user endpoint の writer loop も停止する。
   - fire-and-forget の送信処理は owner の停止 token を渡し、例外をログに残す。
   - Dispose 後の reader/writer entrypoint は no-op にする。

6. 外部入力でメモリと CPU が膨らむ RTPS state を制限する。
   - reliable reader の受信済み sequence number は sliding window と highest contiguous で管理する。
   - GAP の範囲処理は heartbeat window 内に制限し、巨大範囲を個別 SN として追加しない。
   - DATA_FRAG 再構成は sample 数だけでなく総 byte 数も制限する。
   - SEDP writer history は endpoint key ごとの最新状態へ compact し、無制限に保持しない。

## テスト方針

- 変更ごとに、失敗シナリオを先に表すテストを追加する。
- loopback だけでは見えない transport lifecycle は実 UDP テストを追加する。
- RTPS wire-level の修正は bit-exact または submessage 単位のテストを追加する。
- Discovery の修正は endpoint snapshot だけでなく、local writer/reader の matching 状態も確認する。
- 最終確認は `dotnet test rclsharp.sln` と `scripts/check_unity_meta.sh` を実行する。
