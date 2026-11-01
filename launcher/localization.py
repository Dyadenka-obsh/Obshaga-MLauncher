# launcher/localization.py
import json
import sys
from pathlib import Path

def load_lang(lang_code: str) -> dict:
    """Загружает JSON-файл с переводом."""
    if getattr(sys, 'frozen', False):
        base_dir = Path(sys._MEIPASS)
    else:
        base_dir = Path(__file__).resolve().parent.parent

    lang_path = base_dir / "lang" / f"{lang_code}.json"
    
    # Если файла нет, используем английский по умолчанию
    if not lang_path.exists():
        lang_path = base_dir / "lang" / "en.json"

    try:
        with open(lang_path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        print(f"[LANG ERROR] Ошибка загрузки перевода: {e}")
        return {}
