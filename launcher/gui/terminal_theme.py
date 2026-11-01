# launcher/gui/terminal_theme.py
import tkinter as tk
import random
import json
import sys
from pathlib import Path
from PIL import Image, ImageTk

# ---- Значения по умолчанию ----
BG_DARK       = "#68451b"
PANEL_BG      = "#714a1e"
FG_TEXT       = "#f0e6d2"
FG_ACCENT     = "#d4af37"
SCANLINE_CLR  = "#2a1a08"
BORDER_CLR    = "#5a3a15"

FONT_MONO     = ("Consolas", 12)
FONT_MONO_SM  = ("Consolas", 10)
FONT_TITLE    = ("Consolas", 18, "bold")
FONT_HUGE     = ("Consolas", 36, "bold")

LOGO_FILE     = "logo_1973.png"

def load_theme() -> None:
    """Загружает настройки из theme.json в глобальные переменные."""
    global BG_DARK, PANEL_BG, FG_TEXT, FG_ACCENT, SCANLINE_CLR, BORDER_CLR
    global FONT_MONO, FONT_MONO_SM, FONT_TITLE, FONT_HUGE, LOGO_FILE

    if getattr(sys, 'frozen', False):
        base_dir = Path(sys._MEIPASS)
    else:
        base_dir = Path(__file__).resolve().parent.parent.parent

    theme_path = base_dir / "theme.json"
    
    if not theme_path.exists():
        print("[THEME] theme.json не найден, используются стандартные цвета.")
        return

    try:
        with open(theme_path, "r", encoding="utf-8") as f:
            data = json.load(f)
            
            colors = data.get("colors", {})
            BG_DARK = colors.get("bg_dark", BG_DARK)
            PANEL_BG = colors.get("panel_bg", PANEL_BG)
            FG_TEXT = colors.get("fg_text", FG_TEXT)
            FG_ACCENT = colors.get("fg_accent", FG_ACCENT)
            SCANLINE_CLR = colors.get("scanline_clr", SCANLINE_CLR)
            BORDER_CLR = colors.get("border_clr", BORDER_CLR)
            
            fonts = data.get("fonts", {})
            FONT_MONO = tuple(fonts.get("mono", FONT_MONO))
            FONT_MONO_SM = tuple(fonts.get("mono_sm", FONT_MONO_SM))
            FONT_TITLE = tuple(fonts.get("title", FONT_TITLE))
            FONT_HUGE = tuple(fonts.get("huge", FONT_HUGE))
            
            LOGO_FILE = data.get("logo_file", LOGO_FILE)
            print(f"[THEME] Тема загружена. Логотип: {LOGO_FILE}")
    except Exception as e:
        print(f"[THEME ERROR] Ошибка чтения theme.json: {e}")

# Загружаем тему СРАЗУ при импорте
load_theme()

def apply_terminal_style(root: tk.Tk) -> None:
    root.configure(bg=BG_DARK)
    root.title("Aperture Science Development Center")
    root.geometry("1000x600")
    root.minsize(900, 500)

def create_noise_image(width: int, height: int) -> ImageTk.PhotoImage:
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    pixels = img.load()
    for i in range(width):
        for j in range(height):
            if random.random() < 0.05:
                pixels[i, j] = (random.randint(0, 50), random.randint(0, 50), random.randint(0, 50), random.randint(5, 30))
    return ImageTk.PhotoImage(img)

def draw_scanlines(canvas: tk.Canvas, width: int, height: int, step: int = 4) -> None:
    canvas.delete("scanline")
    for y in range(0, height, step):
        canvas.create_line(0, y, width, y, fill=SCANLINE_CLR, tags="scanline", stipple="gray50")
