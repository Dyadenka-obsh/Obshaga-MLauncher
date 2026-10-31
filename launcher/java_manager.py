# launcher/java_manager.py
# Управление JVM. Не качаем Java вручную — используем minecraft-launcher-lib.runtime.

import minecraft_launcher_lib
from launcher.config import JVM_RUNTIME_NAME
from typing import Callable

def ensure_jvm(minecraft_dir: str, log: Callable[[str], None] = print) -> str:
    """
    Проверяет наличие JVM и при необходимости устанавливает её.
    Возвращает путь к java.exe.
    """
    installed = minecraft_launcher_lib.runtime.get_installed_jvm_runtimes(minecraft_dir)

    if JVM_RUNTIME_NAME not in installed:
        log(f"[JVM] Java 21 не найдена. Скачивание рантайма {JVM_RUNTIME_NAME}...")
        log("[JVM] Это ~200 МБ, может занять несколько минут. Не закрывайте окно.")
        minecraft_launcher_lib.runtime.install_jvm_runtime(
            JVM_RUNTIME_NAME, minecraft_dir
        )
        log("[JVM] Установка Java завершена.")

    java_path = minecraft_launcher_lib.runtime.get_executable_path(
        JVM_RUNTIME_NAME, minecraft_dir
    )
    if not java_path:
        raise RuntimeError("Не удалось получить путь к Java после установки JVM.")
    return java_path
