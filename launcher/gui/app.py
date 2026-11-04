# launcher/gui/app.py
import threading
import tkinter as tk
from tkinter import messagebox
import urllib.request
import sys
import uuid
import os
import webbrowser
import shutil
from pathlib import Path
import psutil

from launcher.config import APP_DIR, PACK_CHANGELOG_URL, LAUNCHER_CHANGELOG_URL, DISCORD_INVITE_URL
from launcher.settings import load_settings, save_settings
from launcher.localization import load_lang
from launcher.java_manager import ensure_jvm
from launcher.packwiz import sync_modpack
from launcher.auth_ely import get_ely_profile
from launcher.game_launcher import ensure_game_installed, launch_game
from launcher.discord_rpc import DiscordRPC
from launcher.updater import check_for_updates
from launcher.gui import terminal_theme as theme


class LauncherApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        theme.apply_terminal_style(root)

        self.settings = load_settings()
        self.lang_code = self.settings.get("language", "ru")
        self.trans = load_lang(self.lang_code)
        self.available_langs = ["ru", "en", "uk"]

        self.pack_changelog = None
        self.launcher_changelog = None
        self.current_tab = "pack"

        self.minecraft_dir = str(
            Path.home() / "AppData" / "Roaming" / APP_DIR
            if hasattr(Path.home(), "drive")
            else Path.home() / "." + APP_DIR
        )
        Path(self.minecraft_dir).mkdir(parents=True, exist_ok=True)

        # Discord RPC
        self.rpc = DiscordRPC()
        self.rpc.connect()
        self.rpc.update_status()

        # Проверка обновлений в фоне
        threading.Thread(target=self._check_updates_async, daemon=True).start()

        self.root.protocol("WM_DELETE_WINDOW", self._on_closing)
        self._build_ui()
        self._apply_crt_effects()
        self._apply_language()

    # ------------------------------------------------------------------ UI
    def _build_ui(self) -> None:
        self.bg_canvas = tk.Canvas(self.root, bg=theme.BG_DARK, highlightthickness=0, bd=0)
        self.bg_canvas.place(relx=0, rely=0, relwidth=1, relheight=1)
        
        self.main_frame = tk.Frame(self.root, bg=theme.BG_DARK)
        self.main_frame.place(relx=0.02, rely=0.02, relwidth=0.96, relheight=0.96)
        
        self.main_frame.rowconfigure(1, weight=1)
        self.main_frame.columnconfigure(0, weight=1)
        self.main_frame.columnconfigure(1, weight=0, minsize=300)

        # --- HEADER ---
        header_frame = tk.Frame(self.main_frame, bg=theme.BG_DARK)
        header_frame.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 10))
        
        tk.Label(header_frame, text="Aperture Science Development Center", bg=theme.BG_DARK, fg=theme.FG_TEXT, font=theme.FONT_TITLE).pack(side="left")
        
        self.lbl_project = tk.Label(header_frame, bg=theme.BG_DARK, fg=theme.FG_ACCENT, font=theme.FONT_TITLE)
        self.lbl_project.pack(side="left", padx=20)

        # Настройки и Язык (справа)
        self.settings_btn = tk.Button(
            header_frame, text="⚙", command=self._open_settings,
            bg=theme.BG_DARK, fg=theme.FG_TEXT, activebackground=theme.PANEL_BG, activeforeground=theme.FG_TEXT,
            font=("Segoe UI", 14), relief="flat", bd=0, cursor="hand2"
        )
        self.settings_btn.pack(side="right", padx=10)

        self.lang_btn = tk.Button(
            header_frame, text="", command=self._toggle_language,
            bg=theme.BG_DARK, fg=theme.FG_TEXT, activebackground=theme.PANEL_BG, activeforeground=theme.FG_TEXT,
            font=theme.FONT_MONO_SM, relief="flat", bd=0, highlightthickness=0, cursor="hand2"
        )
        self.lang_btn.pack(side="right")

        # --- LOGS ---
        self.log_text = tk.Text(
            self.main_frame, bg=theme.PANEL_BG, fg=theme.FG_TEXT, font=theme.FONT_MONO_SM,
            relief="flat", highlightthickness=1, highlightbackground=theme.BORDER_CLR,
            state="disabled", wrap="word", padx=10, pady=10
        )
        self.log_text.grid(row=1, column=0, sticky="nsew", padx=(0, 10), pady=(0, 10))

        # --- SIDEBAR ---
        sidebar = tk.Frame(self.main_frame, bg=theme.BG_DARK)
        sidebar.grid(row=1, column=1, sticky="nsew", pady=(0, 10))
        
        if getattr(sys, 'frozen', False):
            base_dir = Path(sys._MEIPASS)
        else:
            base_dir = Path(__file__).resolve().parent.parent.parent

        logo_path = base_dir / theme.LOGO_FILE
        if logo_path.exists():
            try:
                from PIL import Image, ImageTk
                pil_img = Image.open(logo_path).convert("RGBA")
                pil_img.thumbnail((280, 218), Image.Resampling.LANCZOS)
                self.logo_img = ImageTk.PhotoImage(pil_img)
                self.logo_label = tk.Label(sidebar, image=self.logo_img, bg=theme.BG_DARK)
                self.logo_label.pack(anchor="n", pady=(5, 20))
            except: pass

        # --- NEWS FRAME ---
        news_frame = tk.Frame(sidebar, bg=theme.PANEL_BG, bd=1, relief="solid")
        news_frame.pack(fill="both", expand=True)
        
        tab_frame = tk.Frame(news_frame, bg=theme.PANEL_BG)
        tab_frame.pack(fill="x", padx=10, pady=(10, 0))

        self.btn_tab_pack = tk.Button(tab_frame, command=lambda: self._switch_tab("pack"), bg=theme.BG_DARK, fg=theme.FG_TEXT, relief="flat", font=theme.FONT_MONO_SM, cursor="hand2")
        self.btn_tab_pack.pack(side="left", padx=(0, 5))

        self.btn_tab_launcher = tk.Button(tab_frame, command=lambda: self._switch_tab("launcher"), bg=theme.BG_DARK, fg=theme.FG_TEXT, relief="flat", font=theme.FONT_MONO_SM, cursor="hand2")
        self.btn_tab_launcher.pack(side="left")

        text_container = tk.Frame(news_frame, bg=theme.PANEL_BG)
        text_container.pack(fill="both", expand=True, padx=10, pady=(5, 10))

        self.news_text = tk.Text(text_container, bg=theme.PANEL_BG, fg=theme.FG_TEXT, font=theme.FONT_MONO_SM, relief="flat", bd=0, state="disabled", wrap="word", height=10, width=30)
        self.news_text.pack(side="left", fill="both", expand=True)

        scrollbar = tk.Scrollbar(text_container, command=self.news_text.yview, bg=theme.BG_DARK)
        scrollbar.pack(side="right", fill="y")
        self.news_text.configure(yscrollcommand=scrollbar.set)
        
        threading.Thread(target=self._load_changelogs, daemon=True).start()

        # --- BOTTOM LEFT ---
        profile_frame = tk.Frame(self.main_frame, bg=theme.BG_DARK)
        profile_frame.grid(row=2, column=0, sticky="w")
        
        self.lbl_nick = tk.Label(profile_frame, bg=theme.BG_DARK, fg=theme.FG_TEXT, font=theme.FONT_MONO)
        self.lbl_nick.pack(side="left", pady=(15, 0))
        
        self.nick_entry = tk.Entry(profile_frame, width=20, bg=theme.PANEL_BG, fg=theme.FG_TEXT, font=theme.FONT_MONO, relief="flat", highlightthickness=1, highlightbackground=theme.BORDER_CLR, insertbackground=theme.FG_TEXT)
        self.nick_entry.pack(side="left", padx=10, pady=(15, 0))
        self.nick_entry.insert(0, self.settings.get("last_nickname", "Dyadenka"))
        self.nick_entry.bind("<FocusOut>", self._save_nickname)
        self.nick_entry.bind("<Return>", self._save_nickname)

        auth_frame = tk.Frame(profile_frame, bg=theme.BG_DARK)
        auth_frame.pack(side="left", padx=20, pady=(10, 0))

        self.lbl_auth_type = tk.Label(auth_frame, bg=theme.BG_DARK, fg="#b89e79", font=("Consolas", 9))
        self.lbl_auth_type.pack(anchor="w")

        self.is_pirate = self.settings.get("is_pirate", False)
        self.auth_toggle_lbl = tk.Label(auth_frame, bg=theme.BG_DARK, font=theme.FONT_MONO, cursor="hand2")
        self.auth_toggle_lbl.pack(anchor="w")
        self.auth_toggle_lbl.bind("<Button-1>", self._toggle_auth)

        # --- BOTTOM RIGHT ---
        self.play_btn = tk.Button(self.main_frame, command=self._on_launch_clicked, bg=theme.PANEL_BG, fg=theme.FG_TEXT, activebackground=theme.FG_ACCENT, activeforeground=theme.BG_DARK, font=theme.FONT_HUGE, relief="flat", bd=0, highlightthickness=2, highlightbackground=theme.BORDER_CLR)
        self.play_btn.grid(row=2, column=1, sticky="nsew")
        self.play_btn.bind("<Enter>", self._on_play_enter)
        self.play_btn.bind("<Leave>", self._on_play_leave)

    # ------------------------------------------------------------- Окно настроек
    def _open_settings(self):
        win = tk.Toplevel(self.root)
        win.title("Settings")
        win.geometry("450x450")
        win.configure(bg=theme.BG_DARK)
        win.resizable(False, False)
        win.grab_set()

        # Выделение ОЗУ
        tk.Label(win, text="RAM Allocation (MB):", bg=theme.BG_DARK, fg=theme.FG_TEXT, font=theme.FONT_MONO).pack(pady=(15, 5))
        
        total_ram_mb = int(psutil.virtual_memory().total / (1024 * 1024))
        safe_max_ram = total_ram_mb - 2048
        safe_max_ram = (safe_max_ram // 1024) * 1024
        max_allowed = max(2048, safe_max_ram)

        ram_slider = tk.Scale(
            win, from_=1024, to=max_allowed, resolution=1024, 
            orient="horizontal", bg=theme.PANEL_BG, fg=theme.FG_TEXT, 
            highlightthickness=0, length=350
        )
        
        current_ram = self.settings.get("ram_mb", 4096)
        if current_ram > max_allowed:
            current_ram = max_allowed
        ram_slider.set(current_ram)
        ram_slider.pack()

        tk.Label(win, text=f"(Max safe: {max_allowed} MB)", bg=theme.BG_DARK, fg="#b89e79", font=theme.FONT_MONO_SM).pack()

        # Кнопки
        btn_frame = tk.Frame(win, bg=theme.BG_DARK)
        btn_frame.pack(pady=15, fill="x", padx=20)

        def save_and_close():
            self.settings["ram_mb"] = ram_slider.get()
            save_settings(self.settings)
            win.destroy()

        # --- ТЕМА (СБРОС) ---
        tk.Button(btn_frame, text="Reset Theme to Default", command=self._reset_theme, bg=theme.FG_ACCENT, fg=theme.BG_DARK, relief="flat", font=theme.FONT_MONO).pack(fill="x", pady=5)
        tk.Label(btn_frame, text="(Applies after restart)", bg=theme.BG_DARK, fg="#b89e79", font=theme.FONT_MONO_SM).pack(pady=(0, 10))

        tk.Button(btn_frame, text="Open Mods Folder", command=lambda: os.startfile(Path(self.minecraft_dir) / "mods"), bg=theme.PANEL_BG, fg=theme.FG_TEXT, relief="flat", font=theme.FONT_MONO).pack(fill="x", pady=5)
        
        tk.Button(btn_frame, text="Repair Build (Force Update)", command=self._repair_build, bg="#8b0000", fg="white", relief="flat", font=theme.FONT_MONO).pack(fill="x", pady=5)

        tk.Button(btn_frame, text="Join Discord Server", command=lambda: webbrowser.open(DISCORD_INVITE_URL), bg="#5865F2", fg="white", relief="flat", font=theme.FONT_MONO).pack(fill="x", pady=5)

        tk.Button(win, text="Save & Close", command=save_and_close, bg=theme.FG_ACCENT, fg=theme.BG_DARK, relief="flat", font=theme.FONT_MONO).pack(side="bottom", pady=15)

    def _reset_theme(self):
        """Сбрасывает theme.json к заводским настройкам."""
        if getattr(sys, 'frozen', False):
            base_dir = Path(sys._MEIPASS)
        else:
            base_dir = Path(__file__).resolve().parent.parent.parent

        default_theme = base_dir / "theme_default.json"
        current_theme = base_dir / "theme.json"

        # Если мы в exe, то theme.json лежит рядом с exe, а не в _MEIPASS
        if getattr(sys, 'frozen', False):
            current_theme = Path(sys.executable).parent / "_internal" / "theme.json"

        try:
            if default_theme.exists():
                shutil.copyfile(default_theme, current_theme)
                messagebox.showinfo("Theme", "Theme reset to default! Please restart the launcher.")
            else:
                messagebox.showerror("Theme Error", "theme_default.json not found!")
        except Exception as e:
            messagebox.showerror("Theme Error", f"Failed to reset theme:\n{e}")

    def _repair_build(self):
        mods_dir = Path(self.minecraft_dir) / "mods"
        if mods_dir.exists():
            shutil.rmtree(mods_dir)
        messagebox.showinfo("Repair", "Mods folder cleared. Please restart the game to redownload.")
        self.settings["force_update"] = True
        save_settings(self.settings)

    # ------------------------------------------------------------- Логика
    def _check_updates_async(self):
        has_update, url = check_for_updates()
        if has_update and url:
            def _show_update_dialog():
                res = messagebox.askyesno(
                    "Update Available", 
                    "A new version of Aperture Launcher is available!\nDo you want to download it?"
                )
                if res:
                    webbrowser.open(url)

            try:
                if self.root.winfo_exists():
                    self.root.after(0, _show_update_dialog)
            except Exception:
                pass

    def _apply_language(self) -> None:
        t = self.trans
        self.lbl_project.configure(text=t.get("header_project", "PROJECT: OBSHAGA"))
        self.lang_btn.configure(text=t.get("lang_btn", "LANG"))
        self.lbl_nick.configure(text=t.get("nickname", "Nickname:"))
        self.lbl_auth_type.configure(text=t.get("auth_type", "Pirate / ely.by"))
        self.btn_tab_pack.configure(text=t.get("tab_pack", "PACK"))
        self.btn_tab_launcher.configure(text=t.get("tab_launcher", "LAUNCHER"))
        self._update_auth_label()
        if self.play_btn['state'] == 'normal':
            self.play_btn.configure(text=t.get("play", "PLAY"))
        self._update_news_display()

    def _switch_tab(self, tab_name: str) -> None:
        self.current_tab = tab_name
        self._update_news_display()

    def _update_news_display(self) -> None:
        t = self.trans
        error_msg = t.get("news_error", "No internet...")
        if self.current_tab == "pack":
            text = self.pack_changelog or error_msg
            self.btn_tab_pack.configure(bg=theme.FG_ACCENT, fg=theme.BG_DARK)
            self.btn_tab_launcher.configure(bg=theme.BG_DARK, fg=theme.FG_TEXT)
        else:
            text = self.launcher_changelog or error_msg
            self.btn_tab_pack.configure(bg=theme.BG_DARK, fg=theme.FG_TEXT)
            self.btn_tab_launcher.configure(bg=theme.FG_ACCENT, fg=theme.BG_DARK)

        def _update():
            try:
                self.news_text.configure(state="normal")
                self.news_text.delete("1.0", "end")
                self.news_text.insert("end", text)
                self.news_text.configure(state="disabled")
            except tk.TclError: pass

        try:
            if self.root.winfo_exists(): self.root.after(0, _update)
        except: pass

    def _load_changelogs(self) -> None:
        headers = {'User-Agent': 'Mozilla/5.0'}
        try:
            with urllib.request.urlopen(urllib.request.Request(PACK_CHANGELOG_URL, headers=headers), timeout=5) as r:
                d = r.read().decode('utf-8')
                if d.strip(): self.pack_changelog = d
        except: pass
        try:
            with urllib.request.urlopen(urllib.request.Request(LAUNCHER_CHANGELOG_URL, headers=headers), timeout=5) as r:
                d = r.read().decode('utf-8')
                if d.strip(): self.launcher_changelog = d
        except: pass
        try:
            if self.root.winfo_exists(): self.root.after(0, self._update_news_display)
        except: pass

    def _toggle_language(self) -> None:
        idx = self.available_langs.index(self.lang_code)
        self.lang_code = self.available_langs[(idx + 1) % len(self.available_langs)]
        self.trans = load_lang(self.lang_code)
        self._apply_language()
        self._save_language()

    def _save_language(self) -> None:
        self.settings["language"] = self.lang_code
        save_settings(self.settings)

    def _update_auth_label(self) -> None:
        t = self.trans
        if self.is_pirate:
            self.auth_toggle_lbl.configure(text=t.get("auth_pirate", "No Data (pirate)"), fg=theme.FG_TEXT)
        else:
            self.auth_toggle_lbl.configure(text=t.get("auth_ely", "Aperture Science worker (ely.by)"), fg=theme.FG_ACCENT)

    def _on_play_enter(self, event) -> None:
        if self.play_btn['state'] == 'normal': self.play_btn.configure(bg=theme.FG_ACCENT, fg=theme.BG_DARK)

    def _on_play_leave(self, event) -> None:
        if self.play_btn['state'] == 'normal': self.play_btn.configure(bg=theme.PANEL_BG, fg=theme.FG_TEXT)

    def _on_closing(self) -> None:
        self._save_nickname()
        self._save_language()
        if hasattr(self, 'rpc'): self.rpc.close()
        self.root.destroy()

    def _apply_crt_effects(self) -> None:
        self.root.update_idletasks()
        w, h = self.root.winfo_width(), self.root.winfo_height()
        theme.draw_scanlines(self.bg_canvas, w, h)
        if not hasattr(self, 'noise_img'):
            self.noise_img = theme.create_noise_image(w, h)
            self.bg_canvas.create_image(0, 0, image=self.noise_img, anchor="nw", tags="noise")

    def _save_nickname(self, event=None) -> None:
        nickname = self.nick_entry.get().strip()
        if nickname:
            self.settings["last_nickname"] = nickname
            save_settings(self.settings)

    def _toggle_auth(self, event=None) -> None:
        self.is_pirate = not self.is_pirate
        self._update_auth_label()
        self.settings["is_pirate"] = self.is_pirate
        save_settings(self.settings)

    def log(self, message: str) -> None:
        def _update():
            try:
                self.log_text.configure(state="normal")
                self.log_text.insert("end", message + "\n")
                self.log_text.see("end")
                self.log_text.configure(state="disabled")
            except tk.TclError: pass
        try:
            if self.root.winfo_exists(): self.root.after(0, _update)
        except: pass

    def _on_launch_clicked(self) -> None:
        self._save_nickname()
        self.play_btn.configure(state="disabled", text=self.trans.get("loading", "LOADING..."), bg=theme.PANEL_BG, fg=theme.FG_TEXT)
        threading.Thread(target=self._run_pipeline, daemon=True).start()

    def _run_pipeline(self) -> None:
        try:
            nickname = self.nick_entry.get().strip()
            if not nickname: raise ValueError("Enter nickname.")

            self.log("[SYS] Инициализация протокола...")

            if self.is_pirate:
                self.log(f"[AUTH] Режим: Пиратский. Ник: {nickname}")
                offline_uuid = str(uuid.uuid3(uuid.NAMESPACE_DNS, f"OfflinePlayer:{nickname}")).replace("-", "")
                profile = {"nickname": nickname, "uuid": offline_uuid, "skin_url": ""}
            else:
                self.log(f"[ELY] Запрос профиля {nickname!r}...")
                try:
                    profile = get_ely_profile(nickname)
                    self.log(f"[ELY] UUID: {profile['uuid']}")
                except Exception as e:
                    fallback = messagebox.askyesno("ely.by недоступен", f"Ошибка:\n{e}\n\nЗапустить в пиратском режиме?")
                    if fallback:
                        self.is_pirate = True
                        self.root.after(0, self._update_auth_label)
                        self.settings["is_pirate"] = True
                        save_settings(self.settings)
                        offline_uuid = str(uuid.uuid3(uuid.NAMESPACE_DNS, f"OfflinePlayer:{nickname}")).replace("-", "")
                        profile = {"nickname": nickname, "uuid": offline_uuid, "skin_url": ""}
                    else: raise RuntimeError("Cancelled.")

            self.log("[JVM] Проверка Java 21...")
            java_path = ensure_jvm(self.minecraft_dir, log=self.log)

            self.log("[PACKWIZ] Синхронизация сборки...")
            force_update = self.settings.get("force_update", False)
            if force_update:
                self.log("[PACKWIZ] Принудительная переустановка модов...")
                self.settings["force_update"] = False
                save_settings(self.settings)
            
            sync_modpack(self.minecraft_dir, java_path, log=self.log)

            version_id = ensure_game_installed(self.minecraft_dir, java_path, log=self.log)
            self.log(f"[GAME] Передача управления Minecraft ({version_id})...")
            
            self.rpc.update_status(state_text="Playing on OBSHAGA", details_text=f"Nick: {nickname}")

            ram_mb = self.settings.get("ram_mb", 4096)
            self.log(f"[GAME] Выделено ОЗУ: {ram_mb} MB")
            
            launch_game(
                version_id, self.minecraft_dir, java_path,
                profile["nickname"], profile["uuid"], 
                skin_url=profile.get("skin_url", ""),
                ram_mb=ram_mb,
                log=self.log
            )
            self.log("[SYS] Протокол выполнен. Игра запущена.")
            
        except Exception as exc:
            self.log(f"[ERROR] {exc}")
            try:
                if self.root.winfo_exists(): self.root.after(0, lambda e=exc: messagebox.showerror("ERROR", str(e)))
            except: pass
        finally:
            try:
                if self.root.winfo_exists():
                    self.root.after(0, lambda: self.play_btn.configure(state="normal", text=self.trans.get("play", "PLAY"), bg=theme.PANEL_BG, fg=theme.FG_TEXT))
            except: pass
