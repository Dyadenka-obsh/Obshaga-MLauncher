# launcher/gui/terminal_theme.py
import tkinter as tk
import random
from PIL import Image, ImageTk

# ---- Палитра (Portal 2 Style) ----
# ---- Палитра (Portal 2 Style) ----
BG_DARK       = "#68451b"      # Основной фон
PANEL_BG      = "#714a1e"      # Панели (почти сливаются с фоном, как на макете)
FG_TEXT       = "#f0e6d2"      # Цвет текста
FG_ACCENT     = "#d4af37"      # Акцентный цвет
SCANLINE_CLR  = "#2a1a08"      # Линии сканлайнов
BORDER_CLR    = "#5a3a15"      # Рамки (тоже темные, чтобы не выделялись)
# ---- Шрифты ----
FONT_MONO     = ("Consolas", 12)
FONT_MONO_SM  = ("Consolas", 10)
FONT_TITLE    = ("Consolas", 18, "bold")
FONT_HUGE     = ("Consolas", 36, "bold")

def apply_terminal_style(root: tk.Tk) -> None:
    root.configure(bg=BG_DARK)
    root.title("Aperture Science Development Center")
    root.geometry("1000x600")  # Увеличили окно под новый макет
    root.minsize(900, 500)

def create_noise_image(width: int, height: int) -> ImageTk.PhotoImage:
    """Генерирует статичный шум для эффекта ЭЛТ-монитора."""
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    pixels = img.load()
    for i in range(width):
        for j in range(height):
            # Рандомные прозрачные пиксели для эффекта грязи/шума
            if random.random() < 0.05:
                pixels[i, j] = (random.randint(0, 50), random.randint(0, 50), random.randint(0, 50), random.randint(5, 30))
    return ImageTk.PhotoImage(img)

def draw_scanlines(canvas: tk.Canvas, width: int, height: int, step: int = 4) -> None:
    """Рисует горизонтальные линии сканлайнов на Canvas."""
    canvas.delete("scanline")
    for y in range(0, height, step):
        canvas.create_line(0, y, width, y, fill=SCANLINE_CLR, tags="scanline", stipple="gray50")
