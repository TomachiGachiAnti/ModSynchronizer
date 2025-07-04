import tkinter as tk
from tkinter import filedialog, messagebox, ttk
import os
import requests
import shutil

MODS_JSON_URL = "https://raw.githubusercontent.com/TomachiGachiAnti/ModSynchronizer/refs/heads/main/mods.json"

class ModSyncApp:
    def __init__(self, root):
        self.root = root
        self.root.title("MOD 同期ランチャー")
        self.root.geometry("520x320")

        default_minecraft_path = os.path.join(os.environ.get("APPDATA", ""), ".minecraft")
        self.minecraft_dir = tk.StringVar(value=default_minecraft_path)

        self.create_widgets()

    def create_widgets(self):
        tk.Label(self.root, text="Minecraftのインストールフォルダ（.minecraft）を選択：").pack(pady=10)

        self.entry = tk.Entry(self.root, textvariable=self.minecraft_dir, width=60)
        self.entry.pack()

        browse_btn = tk.Button(self.root, text="フォルダを選択", command=self.browse_folder)
        browse_btn.pack(pady=5)

        sync_btn = tk.Button(self.root, text="MODを同期", command=self.sync_mods)
        sync_btn.pack(pady=10)

        self.progress = ttk.Progressbar(self.root, mode="determinate", length=400)
        self.progress.pack(pady=15)

    def browse_folder(self):
        folder_path = filedialog.askdirectory()
        if folder_path:
            self.minecraft_dir.set(folder_path)

    def sync_mods(self):
        minecraft_dir = self.minecraft_dir.get()
        mod_dir = os.path.join(minecraft_dir, "mods")

        if not os.path.isdir(mod_dir):
            messagebox.showerror("エラー", "正しいMinecraftフォルダ（modsフォルダがある場所）を選択してください。")
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

            self.progress["value"] = self.progress["maximum"]

            summary = (
                f"✅ ダウンロード: {len(downloaded)} 個\n" + "\n".join(downloaded) +
                f"\n\n🗑️ 削除: {len(deleted)} 個\n" + "\n".join(deleted)
            )
            messagebox.showinfo("同期完了", summary)

        except Exception as e:
            messagebox.showerror("エラー", f"同期中にエラーが発生しました:\n{e}")
        finally:
            self.progress["value"] = 0

    def download_mod(self, url, dest_path):
        res = requests.get(url, stream=True)
        res.raise_for_status()
        with open(dest_path, "wb") as f:
            shutil.copyfileobj(res.raw, f)

if __name__ == "__main__":
    root = tk.Tk()
    app = ModSyncApp(root)
    root.mainloop()
