# ModSynchronizer 管理メモ

## 目的
このリポジトリは、Minecraft サーバー参加用のクライアント環境を配布 `.exe` で構築・更新するための Windows アプリを管理する。

利用者向けの目標は以下。

- 配布された `.exe` を実行する
- 必要な構成更新を自動で行う
- 必要に応じてアプリ本体も更新する
- 公式 Minecraft ランチャーを起動する
- 利用者は追加済み構成を選んで遊ぶ

認証は公式ランチャーに任せる。

## 合意済み仕様
### 基本目的
- 非技術者でもサーバー参加に必要な環境を短い手順で整えられるようにする
- 利用者に MOD ローダー、互換レイヤー、フォルダ配置などの内部事情を意識させない
- サーバー提供側が検証済みの構成を、そのままクライアントへ配布できるようにする

### 利用者体験
利用者に期待する操作は以下。

1. 配布された構成専用 `.exe` を実行する
2. `.exe` が必要な更新・同期・構成整備を行う
3. `.exe` が公式 Minecraft ランチャーを起動する
4. 利用者は追加済み構成を選択してゲームを起動する
5. 利用者は通常通りサーバーへ参加する

次回以降も、ゲーム起動前にまず同じ `.exe` を実行する運用を想定する。

補足:

- 対象ローダーが未導入なら、本アプリがその状態を検知する
- 対象バージョン本体が未ダウンロードでも、まずはローダー導入を試みる
- 目標は、MOD サーバー参加に必要な準備のほぼすべてを自動または半自動で進めること

### 配布単位
- 配布単位は `Minecraft バージョン` ではなく `構成`
- 同じ `1.21.1 + NeoForge` でも MOD コンセプトが違えば別構成として扱う
- 配布物は構成ごとに別 `.exe` とする

例:

- `Industrial_1.21.1_Setup.exe`
- `Legacy_1.12.2_Setup.exe`

### 対象構成
- 現在の新規配布対象は `Minecraft 1.21.1 + NeoForge`
- Fabric 系要素を内部採用する可能性はある
- ただし Fabric 系要素は提供側が事前検証し、通ったものだけを NeoForge 構成へ組み込む
- 利用者向けには Fabric や Connector の事情は見せない

### 認証方針
- Microsoft アカウント認証は自前実装しない
- 認証は公式 Minecraft ランチャーに委譲する
- 本アプリは専用ランチャーではなく、`構成セットアップ兼更新ツール` として扱う

### 本アプリの責務
- 構成ファイルの読込
- 実行環境の確認
- Minecraft 本体の存在確認
- 対象 Minecraft バージョンの存在確認
- 対象 ModLoader の導入有無確認
- 必要に応じた ModLoader 導入処理
- 専用ゲームディレクトリの作成または更新
- MOD の同期
- `config` など管理対象ファイルの同期
  - `config` は毎回全量上書きではなく、変更が必要なものだけ同期する方針
- GitHub 上の構成ファイル取得
- 公式ランチャー構成の作成または更新
- GitHub Releases 確認
- 必要に応じたアプリ本体自己更新
- 公式 Minecraft ランチャーの起動

### 本アプリの対象外
- `servers.dat` の自動反映
- Microsoft 認証の自前実装
- 利用者による構成編集
- 利用者によるローダー切り替え
- 汎用ランチャー製品化

### 既存 1.12.2 の扱い
- 旧 1.12.2 構成は破棄しない
- 現時点では再利用用の構成情報として保持する
- 必要になれば再度配布構成として復活できるようにする

### 構成管理原則
- 構成の主キーは `config_id`
- `minecraft_version` と `loader` は構成の属性であり主キーではない
- 構成ごとに専用ゲームディレクトリを持たせる
- `.minecraft` 直下の共用 `mods` を直接使う設計にはしない
- 専用ゲームディレクトリは `%APPDATA%\\.modded-minecraft\\<game_directory.name>` に配置する

### データ源の考え方
- 配布用 profile の同期項目は URL ベースで管理する
- 配布 `.exe` に同期対象ファイルや構成情報を埋め込まない
- GitHub 上の profile と配布可能な URL を正とする
- ハッシュ検証可能な形へ寄せる

## 現在の方針
- 実装言語は C#
- UI は WinForms
- 配布単位は Minecraft バージョンではなく `構成`
- `.minecraft` 直下の共用 `mods` は使わず、構成ごとの専用ゲームディレクトリを使う
- 専用ゲームディレクトリは `%APPDATA%\\.modded-minecraft` 配下へ分離する
- Fabric 由来要素は内部事情として扱い、利用者向けには見せない

## 構成単位
同じ `1.21.1 + NeoForge` でも、MOD コンセプトが違えば別構成として扱う。

構成の主キー:

- `config_id`

構成の属性:

- `display_name`
- `minecraft_version`
- `loader`
- `game_directory`
- `launcher`
- `server_setup`
- `mods`
- `files`
- `directories`
- `servers`

## 現在のファイル構成
```text
E:\project\ModSynchronizer\
  ModSynchronizer.sln
  PROJECT_STATUS.md
  profiles\
    legacy-1.12.2.json
    industrial-1.21.1.json
  src\
    ModSynchronizer.Core\
    ModSynchronizer.App\
  assets\
    legacy-1.12.2\
      scripts\
    industrial-1.21.1-server\
      config\
  tools\
    publish-single-file.ps1
    setup-server.sh
    sync-server-profile.sh
  readme.md
```

## 現在の実装状況
### 完了
- C# ソリューション作成
- `ModSynchronizer.Core` 作成
- `ModSynchronizer.App` 作成
- WinForms の最小画面作成
- profile JSON 読込
- 専用ゲームディレクトリ解決
- MOD 同期の骨組み作成
- 管理ファイル同期の骨組み作成
- URL ダウンロード対応
- URL ベース同期対応
- Minecraft 本体存在確認
- 対象 Minecraft バージョン存在確認
- ModLoader 導入済み判定の骨組み
- ModLoader インストーラー取得の骨組み
- NeoForge インストーラー自動実行
- Java 実行環境の探索
- Java 未検出時の一時 JRE 取得フォールバック
- 公式ランチャー profile 作成・更新
- 公式ランチャー起動
- 旧 Python 実装の削除
- 1.12.2 構成の profile 化
- 1.12.2 CraftTweaker スクリプトの GitHub raw URL 化
- 1.21.1 候補 MOD 群の profile 化
- 1.21.1 の `config` ディレクトリ取り込み
- 1.21.1 profile の命名整理
- 1.21.1 MOD の SHA-256 付与
- 1.21.1 MOD の URL 化
- `source_path` の profile からの除去
- `mystcraft_reborn` の 1.21.1 profile からの除外
- `MisutoCraft` を現行 profile から一旦除外
- 単一ファイル publish スクリプト追加
- 構成別 exe 名から GitHub 上の profile を自動取得する処理追加
- 構成別 exe 名から対象 profile を自動選択する処理追加
- GitHub Releases ベース自己更新対応
- Ubuntu 向けサーバー簡易同期スクリプト追加
  - `jq` 非依存の簡易 JSON 抽出版
- Ubuntu 向けサーバー一撃構築スクリプト追加
  - repo clone
  - NeoForge サーバー導入
  - mods/config 同期
  - EULA 確認
  - tmux + systemd 構成
  - `/mnt/hdd/backup/<profile>` への差分バックアップ構築
  - バックアップ用 `systemd timer` 登録
  - `tmux` への `save-all` 自動実行 timer 登録

### 未完了
- NeoForge 導入失敗時の再試行導線整備
- 1.21.1 の正式採用/除外整理
- エラー表示と再試行導線の強化
- `config` 実データの配布内容整理
- GitHub Releases の実運用アセット整備

## 現在の profile
### `profiles/legacy-1.12.2.json`
用途:

- 旧 1.12.2 Forge 系構成の保存

状態:

- 旧 `mods.json` の内容を profile へ移植済み
- `deprecated` 情報も保持済み
- `scripts/ic2.zs` と `scripts/matter.zs` は GitHub raw URL から取得する形で `files` に移植済み
- 運用用スクリプトを `tools` へ分離

注意:

- 現在は構成保存が主目的
- 即運用より、将来再同期できるように残している

### `profiles/industrial-1.21.1.json`
用途:

- 1.21.1 NeoForge 系の候補構成

状態:

- 現在は 65 件の MOD エントリを保持
- 65 件すべてに `url` を設定済み
- すべての MOD に SHA-256 を付与済み
- `server_setup` に vanilla server.jar URL と server 用 config 配置先を設定済み
- Fabric 由来候補や Connector 系には `compat-layer` タグを付与済み
- ランチャー構成作成に必要な `version_id` などを設定済み
- `mystcraft_reborn` は構成から除外済み
- `MisutoCraft` は別対応前提のため、現行構成から一旦除外済み

注意:

- まだ正式採用リストではなく候補状態
- `files` は未整備
- 現時点では `config` の配布必須差分は未設定
- URL の到達性と SHA-256 整合は概ね確認済み
- `config` は差分同期前提で運用する
- 1.21.1 工業メイン構成では、現時点で配布必須の `config` 差分は未設定
- 現在の配布対象 MOD エントリは 65 件

## 主要コード
### コア
- [ProfileLoader.cs](E:\project\ModSynchronizer\src\ModSynchronizer.Core\Services\ProfileLoader.cs)
  - profile 読込と基本検証
- [PathResolver.cs](E:\project\ModSynchronizer\src\ModSynchronizer.Core\Services\PathResolver.cs)
  - `.minecraft` と `%APPDATA%\\.modded-minecraft` の解決
- [SyncService.cs](E:\project\ModSynchronizer\src\ModSynchronizer.Core\Services\SyncService.cs)
  - MOD と管理ファイルの同期
  - ディレクトリ同期
- [MinecraftEnvironmentService.cs](E:\project\ModSynchronizer\src\ModSynchronizer.Core\Services\MinecraftEnvironmentService.cs)
  - Minecraft 本体確認
  - 対象バージョン確認
  - 対象 ModLoader 確認
- [LoaderPreparationService.cs](E:\project\ModSynchronizer\src\ModSynchronizer.Core\Services\LoaderPreparationService.cs)
  - ModLoader インストーラー取得
  - ModLoader インストーラー実行
- [JavaRuntimeResolver.cs](E:\project\ModSynchronizer\src\ModSynchronizer.Core\Services\JavaRuntimeResolver.cs)
  - `PATH` 上の Java 探索
  - Minecraft Launcher 同梱 Java 探索
- [SetupRunner.cs](E:\project\ModSynchronizer\src\ModSynchronizer.Core\Services\SetupRunner.cs)
  - 確認フェーズ
  - 導入フェーズ
  - 構成フェーズ
  - 起動フェーズ
- [LauncherService.cs](E:\project\ModSynchronizer\src\ModSynchronizer.Core\Services\LauncherService.cs)
  - `launcher_profiles.json` 更新
  - 公式ランチャー起動

### UI
- [MainForm.cs](E:\project\ModSynchronizer\src\ModSynchronizer.App\Forms\MainForm.cs)
  - profile 選択
  - セットアップ開始
  - 進捗表示
  - 結果表示
- [ProfileCatalogService.cs](E:\project\ModSynchronizer\src\ModSynchronizer.App\Services\ProfileCatalogService.cs)
  - 構成別 exe 名から対象 profile 名を解決
  - GitHub raw 上の profile を取得してローカルキャッシュする
  - 開発実行時はローカル `profiles` を列挙する

### サーバー運用
- [setup-server.sh](E:\project\ModSynchronizer\tools\setup-server.sh)
  - Ubuntu 上での一撃セットアップ
  - `minecraft` ユーザー作成
  - `/opt/minecraft/<profile>` 配置
  - NeoForge 導入
  - `systemd` サービス化
  - `/mnt/hdd/backup/<profile>` へのバックアップ設定
  - バックアップ用 `systemd timer` 設定
  - `save-all` 用 `systemd timer` 設定
- [sync-server-profile.sh](E:\project\ModSynchronizer\tools\sync-server-profile.sh)
  - GitHub 上の profile と server 用 config を同期
  - クライアント専用 MOD を除外

## 現在の同期方式
### 確認フェーズ
- `.minecraft` の存在を確認する
- 対象 Minecraft バージョンの `versions/<version>` を確認する
- 対象 ModLoader の `versions/<loader-version-id>` を確認する

補足:

- 対象 Minecraft バージョンの存在確認は行うが、未取得でも即エラー終了にはしない
- まず NeoForge 導入を試し、その後に公式ランチャーで必要な本体取得が進む想定とする

### 導入フェーズ
- 現状は ModLoader 未導入時にインストーラー取得と自動実行まで対応
- 導入に失敗した場合は手動実行用パスを案内する

### Java 解決
- まず `PATH` 上の `java` を探す
- 見つからなければ Microsoft Store 版ランチャー同梱 runtime を探す
- 現在の探索候補は `java-runtime-delta` と `jre-legacy`

### MOD
- `url` からダウンロードする
- `sha256` が入っていれば検証
- `deprecated=true` かつ削除方針有効なら削除

### files
- `path` を専用ゲームディレクトリ配下の相対パスとして扱う
- `url` で同期する
  - 現在の 1.21.1 profile では `files` 未使用

### directories
- `url` ベースの同期対象だけを持つ方針とする
- 現在の 1.21.1 profile では `directories` は未使用
- 運用方針として、`config` は全量同期ではなく必要差分のみを持つ

### 専用ゲームディレクトリ
- Minecraft 本体確認や `versions` 確認には `%APPDATA%\\.minecraft` を使う
- 実際の構成別ゲームディレクトリは `%APPDATA%\\.modded-minecraft\\<game_directory.name>` を使う
- その配下に `mods` `config` などを構成ごとに分離して配置する

## 現在のランチャー連携
- `launcher_profiles.json` に構成を作成または更新する
- profile のキーは `launcher.profile_id` を使う
- `lastVersionId` は `launcher.version_id` を使う
- `gameDir` は専用ゲームディレクトリを使う
- セットアップ完了後に公式 Minecraft ランチャーを起動する

## 現在の構成取得方式
- 配布 `.exe` に MOD 構成そのものは埋め込まない
- 構成別 exe 名から対象 profile 名を決定する
  - 例: `industrial-1.21.1-Setup.exe` は `industrial-1.21.1.json` を対象にする
- 対象 profile は GitHub raw の `profiles/<profile>.json` から取得する
- 取得した profile は `%TEMP%\\ModSynchronizer\\profiles-cache` に保存する
- GitHub 取得に失敗した場合は、キャッシュ済み profile があればそれを使う
- 開発時に `-Setup` 名ではない実行ファイルから起動した場合のみ、ローカル `profiles` フォルダを読む

## 現在のサーバー構築方式
- `tools/setup-server.sh` を `wget/curl` で取得して実行する想定
- スクリプトは GitHub から repo を clone して profile を読む
- 対象は現状 `industrial-1.21.1` のみ
- profile の `server_setup.server_jar_url` を使って vanilla `server.jar` を取得する
- profile の `loader.installer_url` を使って NeoForge server を導入する
- `profiles/industrial-1.21.1.server-excludes.txt` でクライアント専用 MOD を除外する
- `assets/industrial-1.21.1-server/config` を server 用 config 同期元として使う
- `minecraft` ユーザーを作成し、配置先は `/opt/minecraft/<profile>` とする
- `tmux` セッション名は profile 名の `.` を `_` に置換した名前を使う
  - 例: `industrial-1.21.1` は `industrial-1_21_1`
- `systemd` サービス名は `minecraft-<profile>.service` とする
- バックアップ先は `/mnt/hdd/backup/<profile>` 固定とする
- バックアップは差分方式で保持し、`latest` シンボリックリンクを更新する
- バックアップ保持期間は 14 日とする
- バックアップ用手動スクリプトを `/usr/local/bin/minecraft-backup-<profile>.sh` に配置する
- バックアップ用 `systemd timer` を登録し、1 時間ごとに自動実行する
- `save-all` 用手動スクリプトを `/usr/local/bin/minecraft-saveall-<profile>.sh` に配置する
- `save-all` 用 `systemd timer` を登録し、3 分ごとに自動実行する

## 現在の自己更新方式
- profile の `self_update.enabled` と `self_update.github_releases_api_url` で有効化する
- 起動時に GitHub Releases を取得する
- ダウンロード後、別 PowerShell プロセスで現在の exe を差し替える
- 差し替え後に更新済み exe を自動再起動する

GitHub Releases 使用時:

- `https://api.github.com/repos/<owner>/<repo>/releases/latest` を参照する
- `tag_name` を現在バージョンと比較する
- `assets[].browser_download_url` から対象 exe を選ぶ
- `asset_name` があればそれを優先し、なければ現在の exe 名と同名アセットを優先する
- さらに見つからなければ最初の `.exe` アセットを使う

## 現在の制約
- NeoForge 導入は CLI 実行を試みるが、環境依存で失敗した場合は手動対応が必要
- `config` の差分管理ルールは今後 profile 運用で詰める

## 対象外
- `servers.dat` の自動反映

## 旧 ModSynchronizer の扱い
- 旧 Python ベースの `ModSynchronizer` はサポート終了
- 旧 `mods.json` と `tool.html` は削除済み
- 旧 CraftTweaker スクリプトは `assets/legacy-1.12.2/scripts` に保持しつつ、配布時は GitHub raw URL から取得する
- 1.12.2 構成は C# 版 profile で保持する

## 次にやる候補
優先度高:

1. 1.21.1 候補 MOD の正式採用/除外を整理する
2. `industrial-1.21.1.json` の `config` を変更対象だけ持つ形へ整理する
3. 実運用で使う URL 群の最終確認を行う

優先度中:

1. ランチャー profile 更新時の安全策を足す
2. `config` の配布元管理方法を固める

優先度低:

1. UI を整える
2. 構成別ビルドフローを整える
3. 配布用 publish 形式を固定する

## ビルド確認
現在のビルド確認コマンド:

```powershell
dotnet build E:\project\ModSynchronizer\ModSynchronizer.sln
```

現状はビルド成功状態。

## 配布物の状態
- アプリ本体コードはビルド成功状態
- 構成別の単一ファイル publish スクリプトを追加済み
- `tools/publish-single-file.ps1 -ProfileFile industrial-1.21.1.json` で publish 済み
- 現在の出力先は `publish/industrial-1.21.1/industrial-1.21.1-Setup.exe`
- publish 出力は単体 exe で起動確認済み
- publish された exe は GitHub 上の profile を取得して同期する想定
