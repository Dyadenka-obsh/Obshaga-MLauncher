using System;

namespace LauncherC.Core
{
    /// <summary>
    /// Глобальная конфигурация и константы лаунчера Aperture Science LauncherC.
    /// Переписано с Python (launcher/config.py) для C# / .NET 10 WPF.
    /// 
    /// Все ключевые настройки проекта OBSHAGA собраны здесь для удобства форков и кастомизации.
    /// </summary>
    public static class Config
    {
        #region Игровая сборка (Minecraft & NeoForge)

        /// <summary>
        /// Целевая версия Minecraft для сборки проекта OBSHAGA.
        /// </summary>
        public const string MinecraftVersion = "1.21.1";

        /// <summary>
        /// Идентификатор загрузчика модов (NeoForge).
        /// </summary>
        public const string ModLoader = "neoforge";

        /// <summary>
        /// URL манифеста packwiz на GitHub для синхронизации клиентских модов, шейдеров и конфигураций.
        /// </summary>
        public const string PackwizPackTomlUrl = "https://raw.githubusercontent.com/Dyadenka-obsh/Obshaga-MServer/refs/heads/main/pack.toml";

        /// <summary>
        /// Ссылка на релиз официального packwiz-installer-bootstrap.jar
        /// https://github.com/packwiz/packwiz-installer-bootstrap
        /// </summary>
        public const string PackwizBootstrapUrl = "https://github.com/packwiz/packwiz-installer-bootstrap/releases/latest/download/packwiz-installer-bootstrap.jar";

        /// <summary>
        /// Ожидаемый SHA-256 хэш файла packwiz-installer-bootstrap.jar для защиты от подмены.
        /// </summary>
        public const string PackwizBootstrapSha256 = "a8fbb24dc604278e97f4688e82d3d91a318b98efc08d5dbfcbcbcab6443d116c";

        #endregion

        #region Сервер проекта OBSHAGA

        /// <summary>
        /// Хост игрового сервера для автоматического прямого подключения при запуске.
        /// </summary>
        public const string ServerHost = "obshaga.my-craft.cc";

        /// <summary>
        /// Порт игрового сервера.
        /// </summary>
        public const int ServerPort = 37015;

        #endregion

        #region Среда выполнения Java (JVM)

        /// <summary>
        /// Название стандартного рантайма Java 21 от Mojang.
        /// </summary>
        public const string JvmRuntimeName = "java-runtime-delta"; // Java 21 LTS

        /// <summary>
        /// Fallback URL для скачивания официального Adoptium OpenJDK 21 (Temurin) для Windows x64.
        /// </summary>
        public const string AdoptiumJava21Url = "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jdk/hotspot/normal/eclipse";

        #endregion

        #region ely.by Skin System API

        /// <summary>
        /// Базовый адрес API скинов ely.by: https://docs.ely.by/en/skins-system.html
        /// </summary>
        public const string ElybySkinsystemBase = "https://skinsystem.ely.by";

        #endregion

        #region Пути по умолчанию и файлы

        /// <summary>
        /// Название папки приложения по умолчанию внутри %APPDATA%.
        /// Пользователь теперь может переопределить полный путь в настройках!
        /// </summary>
        public const string DefaultAppDirName = "ApertureLauncher";

        /// <summary>
        /// URL списка изменений сборки (changelog.txt)
        /// </summary>
        public const string PackChangelogUrl = "https://raw.githubusercontent.com/Dyadenka-obsh/Obshaga-MServer/main/changelog.txt";

        /// <summary>
        /// URL списка изменений самого лаунчера
        /// </summary>
        public const string LauncherChangelogUrl = "https://raw.githubusercontent.com/Dyadenka-obsh/Obshaga-MLauncher/refs/heads/main/changelog.txt";

        /// <summary>
        /// Текущая версия форка LauncherC (2.0.0)
        /// </summary>
        public const string CurrentVersion = "2.0.0";

        /// <summary>
        /// Репозиторий GitHub для проверки обновлений
        /// </summary>
        public const string GitHubRepo = "Dyadenka-obsh/Obshaga-MLauncher";

        #endregion

        #region Discord Rich Presence & Сообщество

        /// <summary>
        /// ID приложения Discord для интеграции богатого присутствия (Rich Presence)
        /// </summary>
        public const string DiscordClientId = "1567493610700800061";

        /// <summary>
        /// Ссылка на приглашение в Discord-сервер сообщества
        /// </summary>
        public const string DiscordInviteUrl = "https://discord.gg/obshagaf";

        #endregion
    }
}
