# launcher/discord_rpc.py
from pypresence import Presence
import time
import threading
from launcher.config import DISCORD_CLIENT_ID

class DiscordRPC:
    def __init__(self):
        self.rpc = None
        self.start_time = None

    def connect(self):
        def _connect():
            try:
                self.rpc = Presence(DISCORD_CLIENT_ID)
                self.rpc.connect()
                self.start_time = int(time.time())
                print("[DISCORD] RPC подключен.")
            except Exception as e:
                print(f"[DISCORD] Ошибка подключения: {e}")
        
        # Подключаем в отдельном потоке, чтобы не вешать лаунчер
        threading.Thread(target=_connect, daemon=True).start()

    def update_status(self, state_text: str = "In Launcher", details_text: str = "Aperture Science"):
        if not self.rpc: return
        try:
            self.rpc.update(
                state=state_text,
                details=details_text,
                large_image="aperture_logo",
                large_text="Aperture Science Enrichment Center",
                start=self.start_time
            )
        except: pass

    def close(self):
        if self.rpc:
            try: self.rpc.close()
            except: pass
