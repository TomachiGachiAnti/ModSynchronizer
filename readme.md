# TGA Minecraft ModSynchronizer

## 説明
サーバー提供側が検証済みの Minecraft 構成を、利用者向けにセットアップするための Windows アプリです。

## 使い方
Release から対象構成の `.exe` をダウンロードして実行してください。

配布 `.exe` 自体には MOD 構成の本体は埋め込まず、実行時に GitHub 上の `profiles/*.json` を取得して同期します。

## 開発 - Build
- ビルド
```sh
dotnet build ModSynchronizer.sln
```

- 実行
```sh
dotnet run --project src/ModSynchronizer.App/ModSynchronizer.App.csproj
```

## 開発 - ローカル profile テスト
GitHub に push する前に、ローカルの `profiles` フォルダを同期元としてテストできます。

```powershell
$env:MODSYNCHRONIZER_PROFILES_BASE_URL = "E:\project\ModSynchronizer\profiles"
& "E:\project\ModSynchronizer\publish\industrial-1.21.1\industrial-1.21.1-Setup.exe"
```

`sync-only` の確認だけをしたい場合は次を使います。

```powershell
$env:MODSYNCHRONIZER_PROFILES_BASE_URL = "E:\project\ModSynchronizer\profiles"
& "E:\project\ModSynchronizer\publish\industrial-1.21.1\industrial-1.21.1-Setup.exe" --mode sync-only --profile industrial-1.21.1
```

profile だけを更新した場合は publish し直す必要はありません。  
ローカル検証では `MODSYNCHRONIZER_PROFILES_BASE_URL` を repo の `profiles` に向ければ、そのまま最新 JSON を参照します。

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

このスクリプトは以下もまとめて構築します。

- `/opt/minecraft/<profile>` へのサーバー配置
- `tmux + systemd` による起動管理
- `/mnt/hdd/backup/<profile>` への差分バックアップ
- バックアップ用 `systemd timer` の登録
- `tmux` への `save-all` 自動実行 timer の登録

`tmux` セッション名は profile 名の `.` を `_` に置換した名前を使います。  
例: `industrial-1.21.1` は `industrial-1_21_1` になります。

バックアップは 1 時間ごとに自動実行されます。手動実行スクリプトは `/usr/local/bin/minecraft-backup-<profile>.sh` に配置されます。
`save-all` は 3 分ごとに自動実行されます。手動実行スクリプトは `/usr/local/bin/minecraft-saveall-<profile>.sh` に配置されます。
