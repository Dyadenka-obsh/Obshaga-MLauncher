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
            
            # Простое сравнение версий (если тег новее текущего)
            if latest_version and latest_version != CURRENT_VERSION:
                return True, data.get("html_url")
    except Exception as e:
        print(f"[UPDATE ERROR] {e}")
    
    return False, None
