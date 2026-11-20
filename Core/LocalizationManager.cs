using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LauncherC.Core
{
    /// <summary>
    /// Менеджер мультиязычной локализации для LauncherC.
    /// Поддерживает русский (ru), английский (en) и украинский (uk) языки.
    /// Переписано с launcher/localization.py с защитой от Path Traversal и динамическим обновлением.
    /// </summary>
    public class LocalizationManager
    {
        private static readonly HashSet<string> AllowedLangs = new(StringComparer.OrdinalIgnoreCase) { "ru", "en", "uk" };
        private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

        public static LocalizationManager Instance { get; } = new();

        public string CurrentLanguage { get; private set; } = "ru";

        public event Action? LanguageChanged;

        private LocalizationManager()
        {
            SetLanguage("ru");
        }

        /// <summary>
        /// Переключает текущий язык и перезагружает словарь строк.
        /// </summary>
        public void SetLanguage(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode) || !AllowedLangs.Contains(langCode))
            {
                langCode = "ru";
            }

            CurrentLanguage = langCode.ToLowerInvariant();
            _strings.Clear();

            // Загрузка из файла lang/<langCode>.json
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string langPath = Path.Combine(baseDir, "lang", $"{CurrentLanguage}.json");

            if (!File.Exists(langPath))
            {
                // Поиск относительно рабочей директории
                langPath = Path.Combine(Directory.GetCurrentDirectory(), "lang", $"{CurrentLanguage}.json");
            }

            if (File.Exists(langPath))
            {
                try
                {
                    string json = File.ReadAllText(langPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            _strings[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LANG ERROR] Ошибка загрузки {langPath}: {ex.Message}");
                }
            }

            // Fallback дефолты если файл отсутствует
            FillDefaultFallbacks();

            LanguageChanged?.Invoke();
        }

        /// <summary>
        /// Получает локализованную строку по ключу с возможностью подстановки параметров {0}, {max} и т.д.
        /// </summary>
        public string Get(string key, params object[] args)
        {
            if (!_strings.TryGetValue(key, out string? value) || string.IsNullOrEmpty(value))
            {
                value = key;
            }

            if (args != null && args.Length > 0)
            {
                try
                {
                    // Обработка именованных или индексных форматов
                    for (int i = 0; i < args.Length; i++)
                    {
                        value = value.Replace($"{{{i}}}", args[i]?.ToString() ?? "");
                    }
                }
                catch
                {
                    // Игнорируем ошибки форматирования
                }
            }

            return value;
        }

        /// <summary>
        /// Индексатор для быстрого доступа к строкам.
        /// </summary>
        public string this[string key] => Get(key);

        /// <summary>
        /// Циклическое переключение на следующий доступный язык (ru -> en -> uk -> ru).
        /// </summary>
        public string ToggleNextLanguage()
        {
            string next = CurrentLanguage switch
            {
                "ru" => "en",
                "en" => "uk",
                _ => "ru"
            };
            SetLanguage(next);
            return next;
        }

        private void FillDefaultFallbacks()
        {
            void AddIfMissing(string k, string v)
            {
                if (!_strings.ContainsKey(k)) _strings[k] = v;
            }

            AddIfMissing("header_project", "ПРОЕКТ: OBSHAGA");
            AddIfMissing("nickname", "Никнейм:");
            AddIfMissing("auth_type", "Пиратский / ely.by");
            AddIfMissing("auth_ely", "Сотрудник Aperture Science (ely.by)");
            AddIfMissing("auth_pirate", "Анонимный испытуемый (пиратский)");
            AddIfMissing("play", "ИГРАТЬ");
            AddIfMissing("loading", "ЗАГРУЗКА...");
            AddIfMissing("tab_pack", "СБОРКА");
            AddIfMissing("tab_launcher", "ЛАУНЧЕР");
            AddIfMissing("news_loading", "Загрузка...");
            AddIfMissing("news_error", "Нет подключения к интернету");
            AddIfMissing("lang_btn", "RU");
            AddIfMissing("settings_title", "Настройки");
            AddIfMissing("ram_alloc", "Выделение ОЗУ (МБ):");
            AddIfMissing("game_dir_label", "Папка установки Minecraft:");
            AddIfMissing("game_dir_browse", "Обзор...");
            AddIfMissing("game_dir_default", "По умолчанию");
            AddIfMissing("status_ready", "Готов к запуску");
            AddIfMissing("btn_in_game", "В ИГРЕ");
            AddIfMissing("status_in_launcher", "В лаунчере");
            AddIfMissing("error_launch_title", "Ошибка запуска");
            AddIfMissing("error_launch_prefix", "Ошибка запуска Minecraft:");
            AddIfMissing("err_neoforge_not_found", "Не удалось обнаружить установленный NeoForge после завершения работы инсталлятора.");
            AddIfMissing("log_sys_init", "[SYS] Система Aperture Science LauncherC инициализирована.");
        }
    }
}
