# launcher/updater.py
import json
import urllib.request
from typing import Optional, Tuple

try:
    from packaging.version import parse as parse_version
except ImportError:
    # Запасной вариант парсинга версии при отсутствии packaging
    def parse_version(v_str: str):
        cleaned = v_str.lower().lstrip("v").replace("-beta", ".b").replace("-alpha", ".a")
        parts = []
        for segment in cleaned.split("."):
            try:
                parts.append(int(segment))
            except ValueError:
                parts.append(segment)
        return tuple(parts)

from launcher.config import CURRENT_VERSION, GITHUB_REPO


def check_for_updates() -> Tuple[bool, Optional[str], Optional[str]]:
    """
    Проверяет наличие новых релизов на GitHub с помощью корректного сравнения версий (SemVer/PEP 440).
    Возвращает (has_update, release_url, latest_version_string).
    """
    try:
        url = f"https://api.github.com/repos/{GITHUB_REPO}/releases/latest"
        req = urllib.request.Request(
            url,
            headers={
                "User-Agent": "ApertureLauncher",
                "Accept": "application/vnd.github.v3+json",
            },
        )

        with urllib.request.urlopen(req, timeout=5) as response:
            data = json.loads(response.read().decode("utf-8"))
            raw_tag = data.get("tag_name", "").strip()

            if not raw_tag:
                return False, None, None

            latest_clean = raw_tag.lstrip("v")
            current_clean = CURRENT_VERSION.lstrip("v")

            try:
                parsed_latest = parse_version(latest_clean)
                parsed_current = parse_version(current_clean)
                has_update = parsed_latest > parsed_current
            except Exception:
                has_update = latest_clean != current_clean

            if has_update:
                return True, data.get("html_url"), raw_tag

    except Exception as e:
        print(f"[UPDATE ERROR] Ошибка при проверке обновлений: {e}")

    return False, None, None
