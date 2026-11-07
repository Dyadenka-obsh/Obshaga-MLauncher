# launcher/updater.py
import urllib.request
import json
import webbrowser
from launcher.config import CURRENT_VERSION, GITHUB_REPO

def check_for_updates():
    """Проверяет последний релиз на GitHub."""
    try:
        url = f"https://api.github.com/repos/{GITHUB_REPO}/releases/latest"
        req = urllib.request.Request(url, headers={'User-Agent': 'ApertureLauncher'})
        
        with urllib.request.urlopen(req, timeout=5) as response:
            data = json.loads(response.read().decode())
            latest_version = data.get("tag_name", "").replace("v", "")
            
            if not latest_version:
                return False, None, None

            # Убираем приставку -beta, -alpha для сравнения
            current_clean = CURRENT_VERSION.replace("-beta", "").replace("-alpha", "")
            latest_clean = latest_version.replace("-beta", "").replace("-alpha", "")

            # Если текущая версия - бета, а последняя - стабильная (и они совпадают по номеру)
            if ("beta" in CURRENT_VERSION or "alpha" in CURRENT_VERSION) and latest_clean == current_clean:
                return True, data.get("html_url"), latest_version
            
            # Если номера версий отличаются (и это не откат назад)
            if latest_version != CURRENT_VERSION and latest_clean != current_clean:
                return True, data.get("html_url"), latest_version

    except Exception as e:
        print(f"[UPDATE ERROR] {e}")
    
    return False, None, None
