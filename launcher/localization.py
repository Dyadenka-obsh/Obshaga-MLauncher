# launcher/localization.py
import json
import sys
from pathlib import Path

ALLOWED_LANGS = {"ru", "en", "uk"}


def load_lang(lang_code: str) -> dict:
    """Безопасно загружает JSON-файл с переводом, предотвращая Path Traversal."""
    if getattr(sys, 'frozen', False):
        base_dir = Path(sys._MEIPASS)
    else:
        base_dir = Path(__file__).resolve().parent.parent

    lang_dir = (base_dir / "lang").resolve()

    # Очистка и проверка по белому списку
    safe_code = lang_code if lang_code in ALLOWED_LANGS else "en"
    lang_path = (lang_dir / f"{safe_code}.json").resolve()

    # Проверка нахождения внутри каталога lang
    try:
        if not lang_path.is_relative_to(lang_dir) or not lang_path.exists():
            lang_path = lang_dir / "en.json"
    except (ValueError, AttributeError):
        lang_path = lang_dir / "en.json"

    if not lang_path.exists():
        return {}

    try:
        with open(lang_path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        print(f"[LANG ERROR] Ошибка загрузки перевода: {e}")
        return {}
