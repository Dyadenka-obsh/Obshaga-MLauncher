# launcher/auth_ely.py
# Работа с ely.by. Используем официальный skinsystem API: https://docs.ely.by/en/skins-system.html
# Библиотека elyby-api (PyPI) оставлена как опциональная зависимость,
# но для минимизации рисков ниже — прямые HTTP-запросы.

import json
import urllib.request
from typing import Optional

from launcher.config import ELYBY_SKINSYSTEM_BASE


import json
import urllib.error
import urllib.request
from typing import Optional

from launcher.config import ELYBY_SKINSYSTEM_BASE


def get_ely_profile(nickname: str) -> dict:
    """
    Возвращает словарь с полями nickname, uuid, skin_url.
    Бросает ValueError, если игрок не найден на ely.by.
    """
    nickname = nickname.strip()
    if not nickname:
        raise ValueError("Никнейм не может быть пустым.")

    profile_url = f"{ELYBY_SKINSYSTEM_BASE}/profile/{nickname}"

    try:
        with urllib.request.urlopen(profile_url, timeout=10) as resp:
            raw = resp.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as e:
        if e.code in (204, 404):
            raise ValueError(f"Игрок {nickname!r} не найден на ely.by.")
        raise RuntimeError(f"ely.by API вернул {e.code} {e.reason}")
    except urllib.error.URLError as e:
        raise RuntimeError(f"Не удалось подключиться к ely.by: {e.reason}")

    # Пустой ответ = профиль не найден (ely.by возвращает 204 No Content).
    if not raw.strip():
        raise ValueError(
            f"Игрок {nickname!r} не найден на ely.by. "
            f"Проверьте ник или зарегистрируйтесь на https://ely.by"
        )

    try:
        profile = json.loads(raw)
    except json.JSONDecodeError:
        raise RuntimeError(
            f"ely.by вернул не-JSON ответ для {nickname!r}. "
            f"Первые 200 символов: {raw[:200]!r}"
        )

    uuid = profile.get("id", "")
    if not uuid:
        raise RuntimeError(f"ely.by не вернул UUID для {nickname!r}")

    return {
        "nickname": nickname,
        "uuid": uuid,
        "skin_url": f"{ELYBY_SKINSYSTEM_BASE}/skins/{nickname}.png",
    }


def try_get_skin_optional(nickname: str) -> Optional[str]:
    """Мягкая проверка: есть ли скин. Не бросает исключение, если скина нет."""
    try:
        url = f"{ELYBY_SKINSYSTEM_BASE}/skins/{nickname}.png"
        with urllib.request.urlopen(url, timeout=5) as resp:
            if resp.status == 200:
                return url
    except Exception:
        pass
    return None
