# launcher/game_launcher.py
import re
import subprocess
import sys
import threading
from typing import Callable

import minecraft_launcher_lib
from launcher.config import MINECRAFT_VERSION, NEOFORGE_LOADER, SERVER_HOST, SERVER_PORT


def ensure_game_installed(
    minecraft_dir: str, java_path: str, log: Callable[[str], None] = print
) -> str:
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
    ram_mb: int = 4096,
    log: Callable[[str], None] = print,
) -> subprocess.Popen:
    # Валидация объема ОЗУ для исключения инъекций параметров JVM
    try:
        ram_val = int(ram_mb)
        if ram_val < 1024 or ram_val > 65536:
            ram_val = 4096
    except (ValueError, TypeError):
        ram_val = 4096

    extra_jvm_args = []
    # Валидация skin_url перед передачей в параметры JVM
    if skin_url and re.match(r"^https?://[a-zA-Z0-9.\-_/:]+$", skin_url):
        extra_jvm_args.append(f"-Delyby.skinUrl={skin_url}")

    jvm_args = [
        f"-Xmx{ram_val}M",
        "-XX:+UseG1GC",
        "-XX:+ParallelRefProcEnabled",
        "-XX:MaxGCPauseMillis=200",
    ] + extra_jvm_args

    # Санитизация никнейма и uuid
    safe_username = re.sub(r"[^a-zA-Z0-9_]", "", username)[:16] or "Player"
    safe_uuid = re.sub(r"[^a-zA-Z0-9]", "", uuid)

    options = {
        "username": safe_username,
        "uuid": safe_uuid,
        "token": "",
        "executablePath": java_path,
        "jvmArguments": jvm_args,
    }

    log("[GAME] Формирование команды запуска...")
    cmd = minecraft_launcher_lib.command.get_minecraft_command(version_id, minecraft_dir, options)
    cmd.extend(["--server", SERVER_HOST, "--port", str(SERVER_PORT)])

    log("[GAME] Запуск Minecraft...")
    creation_flags = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0

    process = subprocess.Popen(
        cmd,
        cwd=minecraft_dir,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
        bufsize=1,
        creationflags=creation_flags,
    )

    def read_logs():
        ansi_escape = re.compile(r"\x1b\[[0-9;]*m")
        if process.stdout:
            for line in iter(process.stdout.readline, ""):
                if line.strip():
                    log(f"[MC] {ansi_escape.sub('', line.rstrip())}")
            process.stdout.close()

    threading.Thread(target=read_logs, daemon=True).start()
    return process
