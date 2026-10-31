# launcher/settings.py
import json
import os
from pathlib import Path
from launcher.config import APP_DIR

def get_settings_path() -> Path:
    if os.name == 'nt':
        return Path.home() / "AppData" / "Roaming" / APP_DIR / "settings.json"
    else:
        return Path.home() / f".{APP_DIR}" / "settings.json"

def load_settings() -> dict:
    """Загружает настройки из файла."""
    path = get_settings_path()
    if not path.exists():
        return {}
    try:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
            print(f"[SETTINGS] Загружено из {path}: {data}")
            return data
    except Exception as e:
        print(f"[SETTINGS ERROR] Ошибка чтения: {e}")
        return {}

def save_settings(settings: dict) -> None:
    """Сохраняет настройки в файл."""
    path = get_settings_path()
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(settings, f, ensure_ascii=False, indent=4)
        print(f"[SETTINGS] Сохранено в {path}: {settings}")
    except Exception as e:
        print(f"[SETTINGS ERROR] Ошибка записи: {e}")
