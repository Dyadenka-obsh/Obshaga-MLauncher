# launcher/auth_ely.py
# Работа с ely.by. Используем официальный skinsystem API: https://docs.ely.by/en/skins-system.html

import json
import re
import urllib.error
import urllib.parse
import urllib.request
import uuid
from typing import Optional

from launcher.config import ELYBY_SKINSYSTEM_BASE

NICKNAME_PATTERN = re.compile(r"^[a-zA-Z0-9_]{3,16}$")


def validate_nickname(nickname: str) -> str:
    """
    Проверяет валидность никнейма по стандартам Minecraft.
    Возвращает очищенный никнейм или выбрасывает ValueError.
    """
    cleaned = nickname.strip()
    if not cleaned:
        raise ValueError("Никнейм не может быть пустым.")
    if not NICKNAME_PATTERN.match(cleaned):
        raise ValueError(
            "Никнейм должен содержать от 3 до 16 латинских символов, цифр или знаков подчеркивания (_)."
        )
    return cleaned


def get_offline_profile(nickname: str) -> dict:
    """
    Генерирует профиль для оффлайн/пиратского режима.
    Использует стандартный для Minecraft алгоритм детерминированного UUID.
    """
    valid_nick = validate_nickname(nickname)
    offline_uuid = str(
        uuid.uuid3(uuid.NAMESPACE_DNS, f"OfflinePlayer:{valid_nick}")
    ).replace("-", "")
    return {
        "nickname": valid_nick,
        "uuid": offline_uuid,
        "skin_url": "",
    }


def get_ely_profile(nickname: str) -> dict:
    """
    Возвращает словарь с полями nickname, uuid, skin_url из ely.by.
    Бросает ValueError, если игрок не найден или имя некорректно.
    """
    valid_nick = validate_nickname(nickname)
    encoded_nick = urllib.parse.quote(valid_nick)
    profile_url = f"{ELYBY_SKINSYSTEM_BASE}/profile/{encoded_nick}"

    try:
        req = urllib.request.Request(
            profile_url,
            headers={"User-Agent": "ApertureLauncher"}
        )
        with urllib.request.urlopen(req, timeout=10) as resp:
            raw = resp.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as e:
        if e.code in (204, 404):
            raise ValueError(f"Игрок {valid_nick!r} не найден на ely.by.")
        raise RuntimeError(f"ely.by API вернул {e.code} {e.reason}")
    except urllib.error.URLError as e:
        raise RuntimeError(f"Не удалось подключиться к ely.by: {e.reason}")

    # Пустой ответ = профиль не найден (ely.by возвращает 204 No Content).
    if not raw.strip():
        raise ValueError(
            f"Игрок {valid_nick!r} не найден на ely.by. "
            f"Проверьте ник или зарегистрируйтесь на https://ely.by"
        )

    try:
        profile = json.loads(raw)
    except json.JSONDecodeError:
        raise RuntimeError(
            f"ely.by вернул не-JSON ответ для {valid_nick!r}. "
            f"Первые 200 символов: {raw[:200]!r}"
        )

    profile_uuid = profile.get("id", "")
    if not profile_uuid:
        raise RuntimeError(f"ely.by не вернул UUID для {valid_nick!r}")

    return {
        "nickname": valid_nick,
        "uuid": str(profile_uuid).replace("-", ""),
        "skin_url": f"{ELYBY_SKINSYSTEM_BASE}/skins/{encoded_nick}.png",
    }


def try_get_skin_optional(nickname: str) -> Optional[str]:
    """Мягкая проверка: есть ли скин. Не бросает исключение, если скина нет."""
    try:
        valid_nick = validate_nickname(nickname)
        encoded_nick = urllib.parse.quote(valid_nick)
        url = f"{ELYBY_SKINSYSTEM_BASE}/skins/{encoded_nick}.png"
        req = urllib.request.Request(url, headers={"User-Agent": "ApertureLauncher"})
        with urllib.request.urlopen(req, timeout=5) as resp:
            if resp.status == 200:
                return url
    except Exception:
        pass
    return None
