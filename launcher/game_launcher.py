# launcher/game_launcher.py
import subprocess
import threading
import re
import sys
from typing import Callable
import minecraft_launcher_lib
from launcher.config import MINECRAFT_VERSION, NEOFORGE_LOADER, SERVER_HOST, SERVER_PORT

def ensure_game_installed(minecraft_dir: str, java_path: str, log: Callable[[str], None] = print) -> str:
    # (Оставляем как было)
    installed_versions = minecraft_launcher_lib.utils.get_installed_versions(minecraft_dir)
    neoforge_versions = [v["id"] for v in installed_versions if v["id"].startswith("neoforge-")]
    if neoforge_versions:
        return sorted(neoforge_versions)[-1]

    log(f"[GAME] Установка {MINECRAFT_VERSION}...")
    minecraft_launcher_lib.install.install_minecraft_version(MINECRAFT_VERSION, minecraft_dir)
    
    log(f"[GAME] Установка {NEOFORGE_LOADER}...")
    loader = minecraft_launcher_lib.mod_loader.get_mod_loader(NEOFORGE_LOADER)
    return loader.install(MINECRAFT_VERSION, minecraft_dir, java=java_path)

def launch_game(
    version_id: str,
    minecraft_dir: str,
    java_path: str,
    username: str,
    uuid: str,
    skin_url: str = "",
    ram_mb: int = 4096, # <--- НОВЫЙ ПАРАМЕТР
    log: Callable[[str], None] = print,
) -> subprocess.Popen:
    
    extra_jvm_args = []
    if skin_url:
        extra_jvm_args.append(f"-Delyby.skinUrl={skin_url}")

    # Динамическое выделение памяти
    jvm_args = [
        f"-Xmx{ram_mb}M", 
        "-XX:+UseG1GC",
        "-XX:+ParallelRefProcEnabled",
        "-XX:MaxGCPauseMillis=200",
    ] + extra_jvm_args

    options = {
        "username": username,
        "uuid": uuid,
        "token": "",                
        "executablePath": java_path,
        "jvmArguments": jvm_args,
    }

    log("[GAME] Формирование команды запуска...")
    cmd = minecraft_launcher_lib.command.get_minecraft_command(version_id, minecraft_dir, options)
    cmd.extend(["--server", SERVER_HOST, "--port", SERVER_PORT])

    log("[GAME] Запуск Minecraft...")
    creation_flags = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0

    process = subprocess.Popen(
        cmd, cwd=minecraft_dir, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        text=True, encoding='utf-8', errors='replace', bufsize=1, creationflags=creation_flags
    )

    def read_logs():
        ansi_escape = re.compile(r'\x1b\[[0-9;]*m')
        for line in iter(process.stdout.readline, ''):
            if line.strip():
                log(f"[MC] {ansi_escape.sub('', line.rstrip())}")
        process.stdout.close()

    threading.Thread(target=read_logs, daemon=True).start()
    return process
