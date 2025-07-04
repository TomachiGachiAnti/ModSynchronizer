# TGA Industrial Minecraft Server Mod Synchronizer

## 説明
TGA 工業マイクラサーバーのmodを同期するためのアプリです。

## 使い方
Releaseから最新版をダウンロードして実行してください。

## 開発 - Build
- ライブラリをインストール
```sh
pip3 install -r requirements.txt
```

- exe化
```sh
pyinstaller --noconfirm --onefile --windowed app.py
```

## mods.jsonについて
mods.jsonでmodの管理をしています。  
tool.htmlでmods.jsonの作成を簡単に行えます。