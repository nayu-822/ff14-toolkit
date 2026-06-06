# FF14 Toolkit

FINAL FANTASY XIV 向けの補助ツールを、.NET 10 と WPF で構築するプロジェクトです。

## 前提環境

- .NET SDK 10.0.300
- Windows

## 構成

- `src/ff14-toolkit-app/Views`: WPF の画面
- `src/ff14-toolkit-app/ViewModels`: MVVM の ViewModel
- `src/ff14-toolkit-app/Infrastructure`: 再利用する MVVM 基盤コード
- `src/ff14-toolkit-app/DependencyInjection`: DI 登録
- `docs`: 調査結果やマッピング根拠

## 実行コマンド

```powershell
dotnet restore .\FF14Toolkit.sln
dotnet build .\FF14Toolkit.sln
dotnet run --project .\src\ff14-toolkit-app\FF14Toolkit.App.csproj
```
