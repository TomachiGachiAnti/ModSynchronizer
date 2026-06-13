# TGA Minecraft Mod Setup

## 説明
サーバー提供側が検証済みの Minecraft 構成を、利用者向けにセットアップするための Windows アプリです。

## 使い方
Release から対象構成の `.exe` をダウンロードして実行してください。

配布 `.exe` 自体には MOD 構成の本体は埋め込まず、実行時に GitHub 上の `profiles/*.json` を取得して同期します。

## 開発 - Build
- ビルド
```sh
dotnet build ModSetup.sln
```

- 実行
```sh
dotnet run --project src/ModSetup.App/ModSetup.App.csproj
```

## 構成ファイル
構成は `profiles` フォルダ配下の JSON で管理します。

## Ubuntu サーバー構築
Ubuntu サーバー側は Release アセットではなく、repo 上のシェルスクリプトを取得して実行する想定です。

- [setup-server.sh](https://github.com/TomachiGachiAnti/ModSynchronizer/blob/main/tools/setup-server.sh)
- [raw setup-server.sh](https://raw.githubusercontent.com/TomachiGachiAnti/ModSynchronizer/main/tools/setup-server.sh)

```bash
wget -O setup-server.sh https://raw.githubusercontent.com/TomachiGachiAnti/ModSynchronizer/main/tools/setup-server.sh
sudo bash setup-server.sh
```
