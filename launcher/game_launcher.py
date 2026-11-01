# launcher/game_launcher.py
# Установка и запуск Minecraft 1.21.1 + NeoForge.
# Используется minecraft-launcher-lib:
#   - mod_loader для установки NeoForge
#   - command.get_minecraft_command для формирования команды запуска

import subprocess
import threading
import re
import sys
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
    # 1. Сначала проверяем, есть ли уже установленный NeoForge локально (фикс блокировок maven)
    installed_versions = minecraft_launcher_lib.utils.get_installed_versions(minecraft_dir)
    neoforge_versions = [
        v["id"] for v in installed_versions if v["id"].startswith("neoforge-")
    ]
    
    if neoforge_versions:
        version_id = sorted(neoforge_versions)[-1]
        log(f"[GAME] NeoForge уже установлен локально: {version_id}")
        return version_id

    # 2. Если не установлен, качаем
    log(f"[GAME] Проверка установки {MINECRAFT_VERSION}...")
    minecraft_launcher_lib.install.install_minecraft_version(
        MINECRAFT_VERSION, minecraft_dir
    )

    log(f"[GAME] Проверка модлоадера {NEOFORGE_LOADER}...")
    loader = minecraft_launcher_lib.mod_loader.get_mod_loader(NEOFORGE_LOADER)
    
    version_id = loader.install(MINECRAFT_VERSION, minecraft_dir, java=java_path)
    
    log(f"[GAME] Версия готова: {version_id}")
    return version_id


def launch_game(
    version_id: str,
    minecraft_dir: str,
    java_path: str,
    username: str,
    uuid: str,
    skin_url: str = "",  # <--- Добавили параметр для скина
    log: Callable[[str], None] = print,
) -> subprocess.Popen:
    """
    Формирует и запускает команду Minecraft с прямым подключением к серверу.
    """
    # Если передан URL скина, добавляем системное свойство для CustomSkinLoader
    extra_jvm_args = []
    if skin_url:
        extra_jvm_args.append(f"-Delyby.skinUrl={skin_url}")

    options = {
        "username": username,
        "uuid": uuid,
        "token": "",                
        "executablePath": java_path,
        "jvmArguments": JVM_ARGS + extra_jvm_args, # <--- Передаем аргументы
    }

    log("[GAME] Формирование команды запуска...")
    cmd = minecraft_launcher_lib.command.get_minecraft_command(
        version_id, minecraft_dir, options
    )
    
    # Вручную добавляем аргументы для прямого подключения к серверу
    cmd.extend(["--server", SERVER_HOST, "--port", SERVER_PORT])

    log("[GAME] Запуск Minecraft (логи транслируются)...")
    
    # Скрываем консоль Windows
    creation_flags = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0

    # Запускаем с перехватом вывода
    process = subprocess.Popen(
        cmd, 
        cwd=minecraft_dir,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding='utf-8',
        errors='replace',
        bufsize=1,  # Построчная буферизация
        creationflags=creation_flags  # <--- ЭТО ПРЯЧЕТ CMD
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
