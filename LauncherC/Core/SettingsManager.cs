using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LauncherC.Core
{
    /// <summary>
    /// Модель конфигурационных настроек лаунчера.
    /// Сериализуется в settings.json.
    /// </summary>
    public class LauncherSettings
    {
        [JsonPropertyName("last_nickname")]
        public string LastNickname { get; set; } = "Dyadenka";

        [JsonPropertyName("is_pirate")]
        public bool IsPirate { get; set; } = false;

        [JsonPropertyName("language")]
        public string Language { get; set; } = "ru";

        [JsonPropertyName("ram_mb")]
        public int RamMb { get; set; } = 4096;

        [JsonPropertyName("force_update")]
        public bool ForceUpdate { get; set; } = false;

        /// <summary>
        /// НОВЫЙ ПУНКТ: Пользовательский путь к папке установки Minecraft.
        /// Если пуст - используется стандартный путь в %APPDATA%\ApertureLauncher.
        /// </summary>
        [JsonPropertyName("custom_minecraft_path")]
        public string CustomMinecraftPath { get; set; } = "";

        /// <summary>
        /// Пользовательский путь к java.exe / javaw.exe (необязательно).
        /// </summary>
        [JsonPropertyName("custom_java_path")]
        public string CustomJavaPath { get; set; } = "";

        /// <summary>
        /// Выбранная тема оформления (modern / retro).
        /// </summary>
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "modern";
    }

    /// <summary>
    /// Менеджер хранения и загрузки настроек лаунчера (settings.json).
    /// Поддерживает безопасное сохранение, выбор пользовательского пути к Minecraft и валидацию.
    /// </summary>
    public static class SettingsManager
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Возвращает путь к директории хранения Minecraft по умолчанию (%APPDATA%\ApertureLauncher).
        /// </summary>
        public static string GetDefaultMinecraftPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "." + Config.DefaultAppDirName);
            }
            return Path.Combine(appData, Config.DefaultAppDirName);
        }

        /// <summary>
        /// Возвращает активный путь к директории Minecraft (пользовательский, если задан, иначе дефолтный).
        /// Гарантирует создание каталога на диске.
        /// </summary>
        public static string GetActiveMinecraftPath(LauncherSettings? settings = null)
        {
            settings ??= LoadSettings();

            string targetPath;
            if (!string.IsNullOrWhiteSpace(settings.CustomMinecraftPath))
            {
                targetPath = settings.CustomMinecraftPath.Trim();
            }
            else
            {
                targetPath = GetDefaultMinecraftPath();
            }

            try
            {
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS ERROR] Ошибка создания каталога {targetPath}: {ex.Message}");
                // Fallback к стандартному пути при ошибке доступа
                targetPath = GetDefaultMinecraftPath();
                Directory.CreateDirectory(targetPath);
            }

            return targetPath;
        }

        /// <summary>
        /// Возвращает путь к файлу настроек settings.json.
        /// settings.json хранится в дефолтном каталоге программы для сохранения настроек между сменами путей.
        /// </summary>
        public static string GetSettingsFilePath()
        {
            string defaultDir = GetDefaultMinecraftPath();
            if (!Directory.Exists(defaultDir))
            {
                Directory.CreateDirectory(defaultDir);
            }
            return Path.Combine(defaultDir, "settings.json");
        }

        /// <summary>
        /// Загружает настройки из settings.json.
        /// При отсутствии файла или ошибке возвращает объект с дефолтными значениями.
        /// </summary>
        public static LauncherSettings LoadSettings()
        {
            string path = GetSettingsFilePath();
            if (!File.Exists(path))
            {
                var defaults = new LauncherSettings();
                SaveSettings(defaults);
                return defaults;
            }

            try
            {
                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions) ?? new LauncherSettings();
                return settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS ERROR] Не удалось прочитать settings.json: {ex.Message}");
                return new LauncherSettings();
            }
        }

        /// <summary>
        /// Сохраняет настройки в settings.json.
        /// </summary>
        public static void SaveSettings(LauncherSettings settings)
        {
            try
            {
                string path = GetSettingsFilePath();
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS ERROR] Не удалось записать settings.json: {ex.Message}");
            }
        }
    }
}
