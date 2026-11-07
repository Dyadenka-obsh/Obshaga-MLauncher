# launcher/config.py
# Константы проекта. Все важное зашито здесь

# ---- Игровая сборка ----
MINECRAFT_VERSION = "1.21.1"
NEOFORGE_LOADER = "neoforge"          # ID модлоадера для minecraft-launcher-lib[reference:1]
PACKWIZ_PACK_TOML_URL = "https://raw.githubusercontent.com/Dyadenka-obsh/Obshaga-MServer/refs/heads/main/pack.toml"
PACKWIZ_BOOTSTRAP_URL = (
    "https://github.com/packwiz/packwiz-installer-bootstrap/releases/latest/download/"
    "packwiz-installer-bootstrap.jar"
)

# ---- Сервер ----
SERVER_HOST = "185.9.145.7"
SERVER_PORT = "37015"

# ---- JVM ----
# minecraft-launcher-lib сам скачает нужный рантайм по этому имени[reference:2].
JVM_RUNTIME_NAME = "java-runtime-delta" #Java 21

# ---- ely.by ----
ELYBY_SKINSYSTEM_BASE = "https://skinsystem.ely.by"

# ---- Пути ----
APP_DIR = "ApertureLauncher"              # подпапка в %APPDATA% / ~/.local/share
# Ссылки на чейнджлоги
PACK_CHANGELOG_URL = "https://raw.githubusercontent.com/Dyadenka-obsh/Obshaga-MServer/main/changelog.txt"
LAUNCHER_CHANGELOG_URL = "https://raw.githubusercontent.com/Dyadenka-obsh/Obshaga-MLauncher/refs/heads/main/changelog.txt"
#Автообновление
CURRENT_VERSION = "1.2.0"
GITHUB_REPO = "Dyadenka-obsh/Obshaga-MLauncher" 

# Discord
DISCORD_CLIENT_ID = "1567493610700800061"
DISCORD_INVITE_URL = "https://discord.gg/obshagaf" # <--- Ссылка на сервер
