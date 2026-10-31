# launcher/game_launcher.py
# Установка и запуск Minecraft 1.21.1 + NeoForge.
# Используется minecraft-launcher-lib:
#   - mod_loader для установки NeoForge
#   - command.get_minecraft_command для формирования команды запуска

import subprocess
import threading
import re
from typing import Callable

import minecraft_launcher_lib

from launcher.config import (
    MINECRAFT_VERSION,
    NEOFORGE_LOADER,
    SERVER_HOST,
    SERVER_PORT,
)

# Рекомендованные JVM-аргументы для G1GC (стабильность на 1.21.1 + NeoForge).
JVM_ARGS = [
    "-Xmx4G",                     # Выделяем 4 ГБ оперативной памяти (если мало - поставь 6G)
    "-XX:+UseG1GC",
    "-XX:+ParallelRefProcEnabled",
    "-XX:MaxGCPauseMillis=200",
]


def ensure_game_installed(minecraft_dir: str, java_path: str, log: Callable[[str], None] = print) -> str:
    """
    Убеждается, что ванильная версия и NeoForge установлены.
    Возвращает ID установленной версии.
    """
    log(f"[GAME] Проверка установки {MINECRAFT_VERSION}...")
    minecraft_launcher_lib.install.install_minecraft_version(
        MINECRAFT_VERSION, minecraft_dir
    )

    log(f"[GAME] Проверка модлоадера {NEOFORGE_LOADER}...")
    loader = minecraft_launcher_lib.mod_loader.get_mod_loader(NEOFORGE_LOADER)
    
    # Метод install сам возвращает точный ID версии (например, 'neoforge-21.1.xxx')
    version_id = loader.install(MINECRAFT_VERSION, minecraft_dir, java=java_path)
    
    log(f"[GAME] Версия готова: {version_id}")
    return version_id

def launch_game(
    version_id: str,
    minecraft_dir: str,
    java_path: str,
    username: str,
    uuid: str,
    log: Callable[[str], None] = print,
) -> subprocess.Popen:
    """
    Формирует и запускает команду Minecraft с прямым подключением к серверу.
    """
    options = {
        "username": username,
        "uuid": uuid,
        "token": "",                
        "executablePath": java_path,
        "jvmArguments": JVM_ARGS,
    }

    log("[GAME] Формирование команды запуска...")
    cmd = minecraft_launcher_lib.command.get_minecraft_command(
        version_id, minecraft_dir, options
    )
    cmd.extend(["--server", SERVER_HOST, "--port", SERVER_PORT])

    log("[GAME] Запуск Minecraft (логи транслируются)...")
    
    # Запускаем с перехватом вывода
    process = subprocess.Popen(
        cmd, 
        cwd=minecraft_dir,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding='utf-8',
        errors='replace',
        bufsize=1  # Построчная буферизация
    )

    # Функция для чтения логов в фоне
    def read_logs():
        # Регулярка для удаления ANSI-цветов (типа [32m, [0m)
        ansi_escape = re.compile(r'\x1b\[[0-9;]*m')
        
        for line in iter(process.stdout.readline, ''):
            line = line.rstrip()
            if line:
                # Очищаем от цветовых кодов
                clean_line = ansi_escape.sub('', line)
                log(f"[MC] {clean_line}")
        process.stdout.close()

    # Запускаем чтение логов в отдельном потоке (daemon, чтобы он умер вместе с лаунчером)
    threading.Thread(target=read_logs, daemon=True).start()
    
    return process
