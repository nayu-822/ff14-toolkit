# AGENTS.md

このリポジトリは `FF14 Toolkit` のワークスペースです。現在は C# / .NET 10 / WPF を使った Windows デスクトップアプリとして構成されています。

他プロジェクト由来の前提は持ち込まず、このリポジトリの実装と構成を優先して作業してください。

## 命名ルール

- 表示名や文書上のプロジェクト名は `FF14 Toolkit` と書く
- `FF14` の大文字小文字が混在した表記は使わない
- 命名は `FF14...` または `ff14-...` のどちらかに統一する
- C# の名前空間、型名、アセンブリ名では `FF14Toolkit` を使う
- リポジトリ名や物理パスなど小文字が自然な場面では `ff14-toolkit` を使う

## ドキュメントルール

- このプロジェクトのドキュメントは原則として日本語で記述する
- `WHM`、`MNK`、`PvP` のようなゲーム内で一般的な略称は無理に日本語化しない
- ゲーム内アクション名や用語は、日本語クライアントで確認できるものを優先して使う
- `Ruin` のようにゲーム内で日本語名があるものは `ルイン` のように日本語で書く

## 現在の構成

- `FF14Toolkit.sln`: ソリューション
- `src/ff14-toolkit-app/`: WPF アプリ本体
- `src/ff14-toolkit-app/Views`: 画面
- `src/ff14-toolkit-app/ViewModels`: ViewModel
- `src/ff14-toolkit-app/Infrastructure`: MVVM の基盤コード
- `src/ff14-toolkit-app/DependencyInjection`: DI 登録
- `docs/`: 調査メモやマッピング根拠

## 技術前提

- .NET 10
- WPF
- `Microsoft.Extensions.Hosting` を使った DI
- MVVM ベースの構成
- Lumina によるゲームデータ参照

## 作業方針

- まず既存構成に合わせる
- 小さく変更し、不要な抽象化は増やさない
- UI ロジックは可能な限り `ViewModels` へ置く
- `code-behind` には WPF 固有の処理だけを残す
- 共通化が必要になってから `Infrastructure` や新規フォルダを追加する
- 文字列、ウィンドウタイトル、ドキュメントなどの表示上の名称は `FF14` 表記で揃える
- 実行に不要な調査メモはコードではなく `docs/` に置く

## 命名と実装

- C# の型名、メソッド名、プロパティ名は `PascalCase`
- ローカル変数と引数は `camelCase`
- nullable reference types を前提に `null` 安全性を保つ
- `async void` はイベントハンドラ以外で使わない
- 複雑な処理は短いメソッドに分割する
- 意味が伝わる名前を優先し、不必要なコメントは増やさない
- 返却データは基本的に JSON 化しやすい構造を優先する

## 変更時の注意

- `FF14Toolkit.*` 名前空間を基準にする
- 新規コードも `FF14Toolkit.*` を使う
- 表示名、名前空間、ファイル名、プロジェクト名の命名は食い違わせない
- 他プロジェクト向けのルールや依存関係を追加しない

## 確認コマンド

```powershell
dotnet restore .\FF14Toolkit.sln
dotnet build .\FF14Toolkit.sln
dotnet run --project .\src\ff14-toolkit-app\FF14Toolkit.App.csproj
```
