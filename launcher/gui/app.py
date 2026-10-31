# launcher/gui/app.py
import threading
import tkinter as tk
from tkinter import messagebox
import urllib.request
import sys
from pathlib import Path

from launcher.config import APP_DIR, CHANGELOG_URL
from launcher.settings import load_settings, save_settings
from launcher.java_manager import ensure_jvm
from launcher.packwiz import sync_modpack
from launcher.auth_ely import get_ely_profile
from launcher.game_launcher import ensure_game_installed, launch_game
from launcher.gui.terminal_theme import (
    apply_terminal_style, create_noise_image, draw_scanlines,
    BG_DARK, PANEL_BG, FG_TEXT, FG_ACCENT, BORDER_CLR,
    FONT_MONO, FONT_MONO_SM, FONT_TITLE, FONT_HUGE
)


class LauncherApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        apply_terminal_style(root)

        # Загружаем настройки при старте
        self.settings = load_settings()

        self.minecraft_dir = str(
            Path.home() / "AppData" / "Roaming" / APP_DIR
            if hasattr(Path.home(), "drive")
            else Path.home() / "." + APP_DIR
        )
        Path(self.minecraft_dir).mkdir(parents=True, exist_ok=True)

        # Перехватываем закрытие окна, чтобы сохранить ник
        self.root.protocol("WM_DELETE_WINDOW", self._on_closing)

        self._build_ui()
        self._apply_crt_effects()

    # ------------------------------------------------------------------ UI
    def _build_ui(self) -> None:
        self.bg_canvas = tk.Canvas(self.root, bg=BG_DARK, highlightthickness=0, bd=0)
        self.bg_canvas.place(relx=0, rely=0, relwidth=1, relheight=1)
        
        self.main_frame = tk.Frame(self.root, bg=BG_DARK)
        self.main_frame.place(relx=0.02, rely=0.02, relwidth=0.96, relheight=0.96)
        
        self.main_frame.rowconfigure(1, weight=1)
        self.main_frame.columnconfigure(0, weight=1)
        self.main_frame.columnconfigure(1, weight=0, minsize=300)

        # --- HEADER ---
        header_frame = tk.Frame(self.main_frame, bg=BG_DARK)
        header_frame.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 10))
        
        tk.Label(header_frame, text="Aperture Science Development Center", bg=BG_DARK, fg=FG_TEXT, font=FONT_TITLE).pack(side="left")
        tk.Label(header_frame, text="PROJECT: OBSHAGA", bg=BG_DARK, fg=FG_ACCENT, font=FONT_TITLE).pack(side="left", padx=20)

        # --- LOGS ---
        self.log_text = tk.Text(
            self.main_frame, bg=PANEL_BG, fg=FG_TEXT, font=FONT_MONO_SM,
            relief="flat", highlightthickness=1, highlightbackground=BORDER_CLR,
            state="disabled", wrap="word", padx=10, pady=10
        )
        self.log_text.grid(row=1, column=0, sticky="nsew", padx=(0, 10), pady=(0, 10))

        # --- SIDEBAR ---
        sidebar = tk.Frame(self.main_frame, bg=BG_DARK)
        sidebar.grid(row=1, column=1, sticky="nsew", pady=(0, 10))
        
        # ЛОГОТИП
        if getattr(sys, 'frozen', False):
            base_dir = Path(sys._MEIPASS)
        else:
            base_dir = Path(__file__).resolve().parent.parent.parent

        logo_path = base_dir / "logo_1973.png"
        if not logo_path.exists():
            logo_path = base_dir / "logo.png"
            
        if logo_path.exists():
            try:
                from PIL import Image, ImageTk
                pil_img = Image.open(logo_path).convert("RGBA")
                pil_img.thumbnail((280, 218), Image.Resampling.LANCZOS)
                self.logo_img = ImageTk.PhotoImage(pil_img)
                self.logo_label = tk.Label(sidebar, image=self.logo_img, bg=BG_DARK)
                self.logo_label.pack(anchor="n", pady=(5, 20))
            except Exception as e:
                print(f"[ERROR] Ошибка загрузки логотипа: {e}")

        # НОВОСТИ / CHANGELOG
        news_frame = tk.Frame(sidebar, bg=PANEL_BG, bd=1, relief="solid")
        news_frame.pack(fill="both", expand=True)
        
        tk.Label(news_frame, text="NEWS / CHANGELOG:", bg=PANEL_BG, fg=FG_ACCENT, font=FONT_MONO).pack(anchor="w", padx=10, pady=(10, 0))
        
        text_container = tk.Frame(news_frame, bg=PANEL_BG)
        text_container.pack(fill="both", expand=True, padx=10, pady=(5, 10))

        self.news_text = tk.Text(
            text_container, bg=PANEL_BG, fg=FG_TEXT, font=FONT_MONO_SM,
            relief="flat", bd=0, state="disabled", wrap="word", height=10, width=30
        )
        self.news_text.pack(side="left", fill="both", expand=True)

        scrollbar = tk.Scrollbar(text_container, command=self.news_text.yview, bg=BG_DARK)
        scrollbar.pack(side="right", fill="y")
        self.news_text.configure(yscrollcommand=scrollbar.set)
        
        threading.Thread(target=self._load_changelog, daemon=True).start()

        # --- BOTTOM LEFT ---
        profile_frame = tk.Frame(self.main_frame, bg=BG_DARK)
        profile_frame.grid(row=2, column=0, sticky="w")
        
        tk.Label(profile_frame, text="Nickname:", bg=BG_DARK, fg=FG_TEXT, font=FONT_MONO).pack(side="left", pady=(15, 0))
        
        self.nick_entry = tk.Entry(profile_frame, width=20, bg=PANEL_BG, fg=FG_TEXT, font=FONT_MONO, relief="flat", highlightthickness=1, highlightbackground=BORDER_CLR, insertbackground=FG_TEXT)
        self.nick_entry.pack(side="left", padx=10, pady=(15, 0))
        
        # Вставляем сохраненный ник
        default_nick = self.settings.get("last_nickname", "Dyadenka")
        self.nick_entry.insert(0, default_nick)
        
        self.nick_entry.bind("<FocusOut>", self._save_nickname)
        self.nick_entry.bind("<Return>", self._save_nickname)

        # ПЕРЕКЛЮЧАТЕЛЬ АВТОРИЗАЦИИ
        auth_frame = tk.Frame(profile_frame, bg=BG_DARK)
        auth_frame.pack(side="left", padx=20, pady=(10, 0))

        tk.Label(
            auth_frame, text="Пиратский / ely.by", 
            bg=BG_DARK, fg="#b89e79", font=("Consolas", 9)
        ).pack(anchor="w")

        self.is_pirate = self.settings.get("is_pirate", False)
        self.auth_toggle_lbl = tk.Label(
            auth_frame, bg=BG_DARK, font=FONT_MONO, cursor="hand2"
        )
        self.auth_toggle_lbl.pack(anchor="w")
        self.auth_toggle_lbl.bind("<Button-1>", self._toggle_auth)
        self._update_auth_label()

        # --- BOTTOM RIGHT ---
        self.play_btn = tk.Button(
            self.main_frame, text="ИГРАТЬ", command=self._on_launch_clicked,
            bg=PANEL_BG, fg=FG_TEXT, activebackground=FG_ACCENT, activeforeground=BG_DARK,
            font=FONT_HUGE, relief="flat", bd=0, highlightthickness=2, highlightbackground=BORDER_CLR
        )
        self.play_btn.grid(row=2, column=1, sticky="nsew")

    # ------------------------------------------------------------- Логика
    def _on_closing(self) -> None:
        """Сохраняет ник и корректно закрывает приложение."""
        self._save_nickname()
        self.root.destroy()

    def _apply_crt_effects(self) -> None:
        self.root.update_idletasks()
        width, height = self.root.winfo_width(), self.root.winfo_height()
        draw_scanlines(self.bg_canvas, width, height)
        if not hasattr(self, 'noise_img'):
            self.noise_img = create_noise_image(width, height)
            self.bg_canvas.create_image(0, 0, image=self.noise_img, anchor="nw", tags="noise")

    def _load_changelog(self) -> None:
        try:
            with urllib.request.urlopen(CHANGELOG_URL, timeout=5) as response:
                changelog_data = response.read().decode('utf-8')
        except Exception:
            changelog_data = "No internet\nconnection\n(joke...or\nnot?)"

        def _update_news():
            try:
                self.news_text.configure(state="normal")
                self.news_text.delete("1.0", "end")
                self.news_text.insert("end", changelog_data)
                self.news_text.configure(state="disabled")
            except tk.TclError:
                pass # Окно уже закрыто

        try:
            if self.root.winfo_exists():
                self.root.after(0, _update_news)
        except (tk.TclError, RuntimeError):
            pass

    def _save_nickname(self, event=None) -> None:
        """Сохраняет ник в файл."""
        nickname = self.nick_entry.get().strip()
        if nickname:
            self.settings["last_nickname"] = nickname
            save_settings(self.settings)

    def _update_auth_label(self) -> None:
        if self.is_pirate:
            self.auth_toggle_lbl.configure(text="No Data (пиратский)", fg=FG_TEXT)
        else:
            self.auth_toggle_lbl.configure(text="Aperture Science worker (ely.by)", fg=FG_ACCENT)

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
            except tk.TclError:
                pass

        try:
            if self.root.winfo_exists():
                self.root.after(0, _update)
        except (tk.TclError, RuntimeError):
            pass

    def _on_launch_clicked(self) -> None:
        # Сохраняем ник перед запуском
        self._save_nickname()

        self.play_btn.configure(state="disabled", text="ЗАГРУЗКА...")
        threading.Thread(target=self._run_pipeline, daemon=True).start()

    def _run_pipeline(self) -> None:
        try:
            nickname = self.nick_entry.get().strip()
            if not nickname:
                raise ValueError("Введите никнейм оператора.")

            self.log("[SYS] Инициализация протокола...")

            if self.is_pirate:
                self.log(f"[AUTH] Режим: Пиратский. Ник: {nickname}")
                profile = {"nickname": nickname, "uuid": "", "skin_url": ""}
            else:
                self.log(f"[ELY] Запрос профиля {nickname!r}...")
                profile = get_ely_profile(nickname)
                self.log(f"[ELY] UUID: {profile['uuid']}")

            self.log("[JVM] Проверка Java 21...")
            java_path = ensure_jvm(self.minecraft_dir, log=self.log)
            self.log(f"[JVM] Исполняемый файл: {java_path}")

            self.log("[PACKWIZ] Синхронизация сборки с GitHub...")
            sync_modpack(self.minecraft_dir, java_path, log=self.log)

            version_id = ensure_game_installed(self.minecraft_dir, java_path, log=self.log)
            self.log(f"[GAME] Передача управления Minecraft ({version_id})...")
            
            launch_game(
                version_id, self.minecraft_dir, java_path,
                profile["nickname"], profile["uuid"], log=self.log
            )
            self.log("[SYS] Протокол выполнен. Игра запущена.")
            
        except Exception as exc:
            self.log(f"[ERROR] {exc}")
            try:
                if self.root.winfo_exists():
                    self.root.after(0, lambda e=exc: messagebox.showerror("ERROR", str(e)))
            except (tk.TclError, RuntimeError):
                pass
        finally:
            try:
                if self.root.winfo_exists():
                    self.root.after(0, lambda: self.play_btn.configure(state="normal", text="ИГРАТЬ"))
            except (tk.TclError, RuntimeError):
                pass
