import os
import shutil
import threading
import tempfile
import sys
import uuid
import requests
import customtkinter as ctk
from tkinter import filedialog, messagebox
from tkinter import ttk
import subprocess

APP_VERSION = "1.2.3"
MODS_JSON_URL = "https://raw.githubusercontent.com/TomachiGachiAnti/ModSynchronizer/refs/heads/main/mods.json"
GITHUB_RELEASES_API = "https://api.github.com/repos/TomachiGachiAnti/ModSynchronizer/releases/latest"


class ModSynchronizer:
    def __init__(self, root):
        self.root = root
        self.version = APP_VERSION
        self.root.title(f"Mod Synchronizer v{APP_VERSION}")
        self.center_window(600, 250)
        self.root.resizable(False, False)

        default_path = os.path.join(os.environ.get("APPDATA", ""), ".minecraft")
        self.minecraft_dir = ctk.StringVar(value=default_path)

        self.create_widgets()
        threading.Thread(target=self.check_latest_version, daemon=True).start()

    def center_window(self, width: int, height: int):
        screen_width = self.root.winfo_screenwidth()
        screen_height = self.root.winfo_screenheight()
        x = int((screen_width - width) / 2)
        y = int((screen_height - height) / 2)
        self.root.geometry(f"{width}x{height}+{x}+{y}")

    def create_widgets(self):
        ctk.CTkLabel(self.root, text="Minecraftのインストールフォルダ（.minecraft）を選択：").pack(pady=10)

        self.entry = ctk.CTkEntry(self.root, textvariable=self.minecraft_dir, width=480)
        self.entry.pack(pady=(0, 5))

        ctk.CTkButton(self.root, text="フォルダを選択", command=self.browse_folder).pack(pady=5)
        ctk.CTkButton(self.root, text="MODを同期", command=self.sync_mods).pack(pady=10)

        self.progress = ttk.Progressbar(self.root, mode="determinate", length=400)
        self.progress.pack(pady=15)

    def browse_folder(self):
        folder_path = filedialog.askdirectory()
        if folder_path:
            self.minecraft_dir.set(folder_path)

    def check_latest_version(self):
        try:
            res = requests.get(GITHUB_RELEASES_API, timeout=5)
            res.raise_for_status()
            data = res.json()
            latest_version = data.get("tag_name", "").lstrip("v")
            if not latest_version:
                return
            if self.is_newer_version(latest_version, self.version):
                self.show_update_info(latest_version, data.get("html_url", ""))
        except Exception:
            pass

    def is_newer_version(self, v1, v2):
        def parse(v):
            return [int(x) for x in v.split(".") if x.isdigit()]
        p1 = parse(v1)
        p2 = parse(v2)
        l = max(len(p1), len(p2))
        p1 += [0] * (l - len(p1))
        p2 += [0] * (l - len(p2))
        return p1 > p2

    def show_update_info(self, latest_version, url):
        def show():
            msg = f"新しいバージョン v{latest_version} が利用可能です。\n\n自動アップデートを実行しますか？\n(はい:自動更新/いいえ:リリースページを開く)"
            result = messagebox.askyesnocancel("アップデートのお知らせ", msg)
            if result is True:
                asset_url = self.get_latest_exe_url()
                if asset_url:
                    self.update_app(asset_url)
                else:
                    messagebox.showerror("アップデート失敗", "実行ファイルのダウンロードURLが取得できませんでした。")
            elif result is False:
                import webbrowser
                webbrowser.open(url)
        self.root.after(0, show)

    def get_latest_exe_url(self):
        try:
            res = requests.get(GITHUB_RELEASES_API, timeout=5)
            res.raise_for_status()
            data = res.json()
            assets = data.get("assets", [])
            for asset in assets:
                if asset["name"].endswith(".exe"):
                    return asset["browser_download_url"]
        except Exception:
            pass
        return None

    def update_app(self, download_url):
        temp_dir = tempfile.gettempdir()
        exe_path = sys.executable
        exe_name = os.path.basename(exe_path)

        try:
            latest_res = requests.get(GITHUB_RELEASES_API, timeout=5)
            latest_res.raise_for_status()
            latest_version = latest_res.json().get("tag_name", "").lstrip("v")
            if not latest_version:
                messagebox.showerror("アップデート失敗", "バージョン番号の取得に失敗しました。")
                return
        except Exception as e:
            messagebox.showerror("アップデート失敗", f"バージョン情報の取得に失敗しました:\n{e}")
            return

        new_exe_name = f"ModSynchronizer_v{latest_version}.exe"
        new_exe_temp_path = os.path.join(temp_dir, new_exe_name)

        batch_name = f"update_{uuid.uuid4().hex}.bat"
        batch_path = os.path.join(temp_dir, batch_name)

        try:
            res = requests.get(download_url, stream=True, timeout=30)
            res.raise_for_status()
            with open(new_exe_temp_path, "wb") as f:
                shutil.copyfileobj(res.raw, f)
        except Exception as e:
            messagebox.showerror("アップデート失敗", f"ダウンロードに失敗しました:\n{e}")
            return

        # バッチファイルの内容を作成
        batch_script = f"""
taskkill /f /im "{exe_name}" >nul 2>&1
del /f /q "{exe_path}" >nul 2>&1
move /Y "{new_exe_temp_path}" "{exe_path}" >nul 2>&1
call "{exe_path}"
pause
        """

        try:
            with open(batch_path, "w", encoding="utf-8") as f:
                f.write(batch_script)
            # cmd + start でウィンドウ付きで表示
            subprocess.Popen(f'"{batch_path}"', shell=True)

        except Exception as e:
            messagebox.showerror("アップデート失敗", f"アップデートスクリプトの実行に失敗しました:\n{e}")
            return

        self.root.after(0, self.root.quit)

    def sync_mods(self):
        minecraft_dir = self.minecraft_dir.get()
        mod_dir = os.path.join(minecraft_dir, "mods")
        scripts_dir = os.path.join(minecraft_dir, "scripts")

        if not os.path.isdir(mod_dir):
            messagebox.showerror("エラー", "正しいMinecraftフォルダ（modsフォルダがある場所）を選択してください。")
            return

        if not os.path.isdir(scripts_dir):
            try:
                os.makedirs(scripts_dir, exist_ok=True)
            except Exception as e:
                messagebox.showerror("エラー", f"scriptsフォルダの作成に失敗しました:\n{e}")
                return

        try:
            res = requests.get(MODS_JSON_URL)
            res.raise_for_status()
            data = res.json()
            mods_data = data.get("mods", [])
            json_mods = {mod["filename"]: mod for mod in mods_data}

            local_files = {
                f for f in os.listdir(mod_dir)
                if f.endswith(".jar") and os.path.isfile(os.path.join(mod_dir, f))
            }

            downloaded = []
            deleted = []

            total_tasks = len([m for m in json_mods.values() if not m.get("deprecated", False)])
            self.progress["maximum"] = total_tasks
            self.progress["value"] = 0

            for filename, mod in json_mods.items():
                if not mod.get("deprecated", False) and filename not in local_files:
                    url = mod.get("url")
                    dest_path = os.path.join(mod_dir, filename)
                    self.download_mod(url, dest_path)
                    downloaded.append(filename)
                self.progress["value"] += 1
                self.root.update_idletasks()

            mods_to_delete = []
            for filename in local_files:
                if filename in json_mods and json_mods[filename].get("deprecated", False):
                    mods_to_delete.append(filename)

            if mods_to_delete:
                confirm = messagebox.askyesno(
                    "削除の確認",
                    f"以下の廃止MODを削除してもよろしいですか？\n\n" + "\n".join(mods_to_delete)
                )
                if confirm:
                    for filename in mods_to_delete:
                        os.remove(os.path.join(mod_dir, filename))
                        deleted.append(filename)

            recipe_downloaded = []
            try:
                api_url = "https://api.github.com/repos/TomachiGachiAnti/ModSynchronizer/contents/scripts"
                resp = requests.get(api_url)
                resp.raise_for_status()
                scripts_files = resp.json()
                for fileinfo in scripts_files:
                    if fileinfo['name'].endswith('.zs') and fileinfo['type'] == 'file':
                        raw_url = fileinfo['download_url']
                        dest_path = os.path.join(scripts_dir, fileinfo['name'])
                        try:
                            file_resp = requests.get(raw_url)
                            file_resp.raise_for_status()
                            with open(dest_path, 'wb') as f:
                                f.write(file_resp.content)
                            recipe_downloaded.append(fileinfo['name'])
                        except Exception as e:
                            messagebox.showwarning("スクリプト同期エラー", f"{fileinfo['name']} の同期に失敗しました:\n{e}")
            except Exception as e:
                messagebox.showwarning("スクリプト同期エラー", f"GitHubからのスクリプト取得に失敗しました:\n{e}")

            self.progress["value"] = self.progress["maximum"]

            summary = (
                f"✅ ダウンロード: {len(downloaded)} 個\n" + ("\n".join(downloaded) if downloaded else "(なし)") +
                f"\n\n🗑️ 削除: {len(deleted)} 個\n" + ("\n".join(deleted) if deleted else "(なし)") +
                f"\n\n📜 レシピ同期: {len(recipe_downloaded)} 個\n" + ("\n".join(recipe_downloaded) if recipe_downloaded else "(なし)")
            )
            messagebox.showinfo("同期完了", summary)

        except Exception as e:
            messagebox.showerror("エラー", f"同期中にエラーが発生しました:\n{e}")
        finally:
            self.progress["value"] = 0

    def download_mod(self, url: str, dest_path: str):
        res = requests.get(url, stream=True)
        res.raise_for_status()
        with open(dest_path, "wb") as f:
            shutil.copyfileobj(res.raw, f)


if __name__ == "__main__":
    ctk.set_appearance_mode("dark")
    ctk.set_default_color_theme("blue")
    root = ctk.CTk()
    app = ModSynchronizer(root)
    root.mainloop()
