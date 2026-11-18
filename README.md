# Aperture Science Minecraft Launcher (C# Fork — LauncherC v2.0.0)

[![.NET 10](https://img.shields.io/badge/.NET-10.0%20WPF-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/Version-2.0.0-FF9A00)](https://github.com/Dyadenka-obsh/Obshaga-MLauncher)
[![Minecraft](https://img.shields.io/badge/Minecraft-1.21.1%20%2B%20NeoForge-357A38?logo=minecraft)](https://neoforged.net/)
[![Project](https://img.shields.io/badge/Project-OBSHAGA-FF9A00)](https://discord.gg/obshagaf)

Современный форк кастомного лаунчера Minecraft для проекта **OBSHAGA**, переписанный с **Python** на **C# (.NET 10 WPF)**.

---

## 🚀 Основные нововведения версии 2.0.0

1. **Две темы оформления по умолчанию с мгновенным переключением (кнопка `🎨 ТЕМА`)**:
   - **Тема №1 — «Современная» (Modern 2.0)**: глубокая hi-tech тёмная палитра (`#101216`), неоновые акценты Aperture Amber (`#FF9A00`), скругления карточек, градиентная кнопка запуска и системный шрифт Segoe UI.
   - **Тема №2 — «Ретро 1973» (в стиле версии 1.2)**: аутентичный терминал Aperture Laboratories 1970-х годов (`#68451B`, `#714A1E`, золотистый `#D4AF37`), плоские терминальные границы, моноширинный шрифт Consolas, оригинальный логотип `logo_1973.png` и эффект растровых CRT-сканлайнов.
   - Переключение тем доступно в один клик по кнопке в шапке окна или в окне настроек.

2. **Свободное перемещение окна**:
   - Окно больше не залочено по центру: его можно свободно перетаскивать по экрану, зажав левую кнопку мыши в любой свободной области шапки или окна.

3. **Выбор папки установки Minecraft (Game Directory)**:
   - В настройках добавлен выбор пользовательской директории установки (кнопка *«Обзор...»*).
   - Кнопка *«По умолчанию»* возвращает стандартный путь в `%APPDATA%\ApertureLauncher`.
   - Все моды, конфиги, шейдеры и файлы версий сохраняются в выбранном каталоге.

4. **Автообновление (версия 2.0.0)**:
   - Встроенный сервис проверки релизов с GitHub Releases API настроен на версию **2.0.0** и автоматически уведомит пользователя при выходе новых версий.

5. **Надёжная архитектура C# и открытые библиотеки**:
   - **[CmlLib.Core](https://github.com/CmlLib/CmlLib.Core)**: проверенная открытая библиотека для управления версиями Minecraft, формирования JVM-аргументов и запуска процесса.
   - **[DiscordRichPresence](https://github.com/Lachee/discord-rpc-csharp)**: официальный .NET-клиент Discord RPC с отображением игрового статуса и времени.
   - **Packwiz Bootstrap**: загрузка и запуск `packwiz-installer-bootstrap.jar` с валидацией контрольной суммы по SHA-256 (`a8fbb24dc604278e97f4688e82d3d91a318b98efc08d5dbfcbcbcab6443d116c`).
   - **Автоматическая установка Java 21**: поиск системной Java 21+, Mojang `java-runtime-delta` или автоматическая загрузка Adoptium Eclipse Temurin 21 OpenJDK.
   - **Аутентификация ely.by & Оффлайн**:
     - Поддержка официального REST API ely.by Skinsystem.
     - Стандартный для Minecraft детерминированный алгоритм генерации UUID v3 (RFC 4122) для оффлайн/пиратского режима.

6. **Полная мультиязычность**:
   - Русский (`RU`), Английский (`EN`), Украинский (`UK`) с горячим переключением прямо из интерфейса.

---

## 🛠 Технический стек

- **Язык**: C# 13 / .NET 10.0 (Windows Desktop WPF)
- **Библиотеки NuGet**:
  - `CmlLib.Core` (v3.3.9) — ядро запуска Minecraft
  - `DiscordRichPresence` (v1.2.1.24) — интеграция с Discord
  - `System.Text.Json` — быстрая работа с настройками и API

---

## 📂 Структура проекта `LauncherC`

```text
LauncherC/
├── Assets/                        # Иконки и графические ресурсы
│   ├── icon.ico                   # Иконка приложения (Modern)
│   └── logo_1973.png              # Ретро-логотип Aperture 1973 (Retro)
├── Core/                          # Бизнес-логика лаунчера
│   ├── Config.cs                  # Константы (CurrentVersion = 2.0.0, MC 1.21.1, packwiz, сервер)
│   ├── ThemeManager.cs            # Динамическое управление темами (Modern 2.0 и Retro 1973)
│   ├── SettingsManager.cs         # Чтение/сохранение settings.json, выбор пути установки
│   ├── LocalizationManager.cs     # Менеджер языковых пакетов (ru, en, uk)
│   ├── AuthService.cs             # Валидация ника, оффлайн UUID v3, ely.by API
│   ├── JavaManager.cs             # Валидация и автозагрузка Java 21
│   ├── PackwizManager.cs          # Скачивание bootstrap, SHA-256 проверка, запуск синхронизации
│   ├── MinecraftLauncherService.cs # Установка NeoForge 1.21.1 и запуск процесса с JVM аргументами
│   ├── DiscordRpcManager.cs       # Статус присутствия в Discord
│   └── UpdateChecker.cs           # Проверка релизов с GitHub по SemVer (2.0.0)
├── UI/                            # Пользовательский интерфейс (WPF XAML)
│   ├── MainWindow.xaml (.cs)      # Главное окно (терминал, новости, профиль, тема, кнопка PLAY)
│   └── SettingsWindow.xaml (.cs)  # Модальное окно настроек (тема, папка игры, ОЗУ, Java, моды)
├── lang/                          # Языковые файлы локализации
│   ├── ru.json                    # Русский
│   ├── en.json                    # Английский
│   └── uk.json                    # Украинский
├── LauncherC.csproj               # Файл проекта MSBuild
├── LauncherC.slnx                 # Файл решения (Solution)
└── README.md                      # Документация проекта
```

---

## 🔨 Сборка и запуск

### 1. Запуск из исходников
```powershell
cd LauncherC
dotnet run
```

### 2. Сборка проекта в Release
```powershell
dotnet build -c Release
```

### 3. Публикация единого автономного `.exe`
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
Готовый файл `LauncherC.exe` появится в:
`LauncherC/bin/Release/net10.0-windows/win-x64/publish/`
