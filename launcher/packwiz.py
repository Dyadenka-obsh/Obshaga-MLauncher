# launcher/packwiz.py
# Синхронизация сборки через официальный packwiz-installer-bootstrap.

import os
import subprocess
import urllib.request
from pathlib import Path
from typing import Callable

from launcher.config import PACKWIZ_BOOTSTRAP_URL, PACKWIZ_PACK_TOML_URL


def _download_bootstrap(dest: Path) -> None:
    """Скачивает packwiz-installer-bootstrap.jar, если его ещё нет."""
    if dest.exists():
        return
    dest.parent.mkdir(parents=True, exist_ok=True)
    urllib.request.urlretrieve(PACKWIZ_BOOTSTRAP_URL, dest)


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
    _download_bootstrap(jar_path)

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
    for line in process.stdout:
        log(f"[PACKWIZ] {line.rstrip()}")
    process.wait()
    if process.returncode != 0:
        raise RuntimeError(f"packwiz-installer завершился с кодом {process.returncode}")
