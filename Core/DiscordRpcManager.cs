using System;
using DiscordRPC;
using DiscordRPC.Logging;

namespace LauncherC.Core
{
    /// <summary>
    /// Менеджер интеграции с Discord Rich Presence.
    /// Переписано с launcher/discord_rpc.py с использованием официальной библиотеки DiscordRichPresence для .NET.
    /// Отображает статус игрока в Discord (в лаунчере / на сервере OBSHAGA).
    /// </summary>
    public class DiscordRpcManager : IDisposable
    {
        private DiscordRpcClient? _client;
        private Timestamps? _startTime;
        private bool _isInitialized;

        public static DiscordRpcManager Instance { get; } = new();

        private DiscordRpcManager() { }

        /// <summary>
        /// Инициализирует подключение к Discord в неблокирующем фоновом режиме.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                _client = new DiscordRpcClient(Config.DiscordClientId)
                {
                    Logger = new ConsoleLogger { Level = LogLevel.Warning }
                };

                _client.OnReady += (_, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[DISCORD] RPC подключен для пользователя {e.User.Username}");
                };

                _client.OnError += (_, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[DISCORD] Ошибка: {e.Message}");
                };

                _startTime = Timestamps.Now;
                _client.Initialize();
                _isInitialized = true;

                UpdateStatus("В лаунчере", "Aperture Science Development Center");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DISCORD] Не удалось инициализировать RPC: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновляет статус присутствия в Discord.
        /// </summary>
        public void UpdateStatus(string state, string details)
        {
            if (_client == null || !_client.IsInitialized) return;

            try
            {
                _client.SetPresence(new RichPresence
                {
                    State = state,
                    Details = details,
                    Timestamps = _startTime,
                    Assets = new Assets
                    {
                        LargeImageKey = "aperture_logo",
                        LargeImageText = "Aperture Science Enrichment Center"
                    }
                });
            }
            catch
            {
                // Игнорируем ошибки передачи статуса
            }
        }

        /// <summary>
        /// Закрывает подключение к Discord RPC.
        /// </summary>
        public void Dispose()
        {
            if (_client != null)
            {
                try
                {
                    _client.ClearPresence();
                    _client.Dispose();
                    _client = null;
                }
                catch { }
            }
        }
    }
}
