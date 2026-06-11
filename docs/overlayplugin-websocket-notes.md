# OverlayPlugin WebSocket 確認メモ

ACT の `OverlayPlugin WSServer` を `ws://127.0.0.1:10501/ws` で起動した状態で確認した。

## 応答を確認できた call

- `getLanguage`
  - 例: `{"language":"English","languageId":"1","region":"Global","regionId":"1"}`
- `getVersion`
  - 例: `{"version":"0.19.98.0"}`
- `getCombatants`
  - `combatants` 配列で返る
  - 各要素に `Name`, `PosX`, `PosY`, `PosZ`, `Heading`, `CurrentHP`, `WorldName` などを含む
- `loadData`
  - キー未登録時は `{"$isNull":true}` を返した

## request 形式

`type=request` と `rseq` を付けた形式でも応答を確認できた。

```json
{"type":"request","call":"getCombatants","rseq":102}
```

応答側にも `rseq` が含まれる。

## 今回の環境で単純呼び出しでは応答を確認できなかった call

- `getConfig`
- `getEnmity`
- `getEncounter`
- `getPartyList`
- `getCombatData`

未対応とは限らず、別のイベントソースや前提条件が必要な可能性がある。
