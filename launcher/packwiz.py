# launcher/packwiz.py
# Синхронизация сборки через официальный packwiz-installer-bootstrap.

import hashlib
import os
import subprocess
import urllib.request
from pathlib import Path
from typing import Callable

from launcher.config import (
    PACKWIZ_BOOTSTRAP_SHA256,
    PACKWIZ_BOOTSTRAP_URL,
    PACKWIZ_PACK_TOML_URL,
)


def _compute_sha256(file_path: Path) -> str:
    """Вычисляет хэш SHA-256 файла."""
    sha256 = hashlib.sha256()
    with open(file_path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            sha256.update(chunk)
    return sha256.hexdigest().lower()


def _download_bootstrap(dest: Path, log: Callable[[str], None] = print) -> None:
    """Скачивает packwiz-installer-bootstrap.jar с верификацией SHA-256."""
    expected_hash = PACKWIZ_BOOTSTRAP_SHA256.lower()

    if dest.exists():
        actual_hash = _compute_sha256(dest)
        if actual_hash == expected_hash:
            return
        log("[PACKWIZ] Хэш существующего bootstrap-файла некорректен, повторная загрузка...")
        try:
            dest.unlink()
        except OSError:
            pass

    dest.parent.mkdir(parents=True, exist_ok=True)
    temp_path = dest.with_suffix(".tmp")
    log("[PACKWIZ] Загрузка проверенного packwiz-installer-bootstrap...")
    
    req = urllib.request.Request(
        PACKWIZ_BOOTSTRAP_URL,
        headers={"User-Agent": "ApertureLauncher"}
    )
    with urllib.request.urlopen(req, timeout=15) as response, open(temp_path, "wb") as out_file:
        for chunk in iter(lambda: response.read(65536), b""):
            out_file.write(chunk)

    actual_hash = _compute_sha256(temp_path)
    if actual_hash != expected_hash:
        if temp_path.exists():
            temp_path.unlink()
        raise RuntimeError(
            f"Нарушена целостность файла packwiz-installer-bootstrap! "
            f"Ожидался хэш {expected_hash}, получен {actual_hash}"
        )

    temp_path.replace(dest)
    log("[PACKWIZ] Целостность bootstrap-файла успешно подтверждена.")


def sync_modpack(
    minecraft_dir: str,
    java_path: str,
    log: Callable[[str], None] = print,
) -> None:
    """
    Запускает packwiz-installer-bootstrap с параметрами:
      -g          — без GUI
      -s client   — клиентская сторона
      <URL pack.toml>
    """
    jar_path = Path(minecraft_dir) / "packwiz-installer-bootstrap.jar"
    _download_bootstrap(jar_path, log=log)

    cmd = [
        java_path,
        "-jar", str(jar_path),
        "-g",
        "-s", "client",
        PACKWIZ_PACK_TOML_URL,
    ]
    log(f"[PACKWIZ] Запуск: {' '.join(cmd)}")

    process = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        cwd=minecraft_dir,
    )
    if process.stdout:
        for line in process.stdout:
            log(f"[PACKWIZ] {line.rstrip()}")
    process.wait()
    if process.returncode != 0:
        raise RuntimeError(f"packwiz-installer завершился с кодом {process.returncode}")
