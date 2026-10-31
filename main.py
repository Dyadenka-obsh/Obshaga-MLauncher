# main.py
# Точка входа. Запускает Tkinter-приложение.

import tkinter as tk
from launcher.gui.app import LauncherApp


def main() -> None:
    root = tk.Tk()
    LauncherApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
