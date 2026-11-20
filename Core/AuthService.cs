using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LauncherC.Core
{
    /// <summary>
    /// Данные профиля игрока Minecraft (ely.by или оффлайн/пиратский).
    /// </summary>
    public class PlayerProfile
    {
        public string Nickname { get; set; } = "Player";
        public string Uuid { get; set; } = "";
        public string SkinUrl { get; set; } = "";
        public bool IsOffline { get; set; } = false;
    }

    /// <summary>
    /// Сервис аутентификации и профилей.
    /// Переписано с launcher/auth_ely.py.
    /// Реализует:
    /// 1. Валидацию никнейма по стандартам Minecraft (3-16 символов).
    /// 2. Детерминированную генерацию стандартного Mojang UUID v3 для оффлайн-режима.
    /// 3. Работу с официальным REST API ely.by Skin System (https://docs.ely.by/en/skins-system.html).
    /// </summary>
    public static class AuthService
    {
        private static readonly Regex NicknameRegex = new(@"^[a-zA-Z0-9_]{3,16}$", RegexOptions.Compiled);
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        static AuthService()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"ApertureLauncher/{Config.CurrentVersion}");
        }

        /// <summary>
        /// Проверяет валидность никнейма. Выбрасывает ArgumentException, если ник некорректен.
        /// </summary>
        public static string ValidateNickname(string? nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                throw new ArgumentException("Никнейм не может быть пустым.");
            }

            string clean = nickname.Trim();
            if (!NicknameRegex.IsMatch(clean))
            {
                throw new ArgumentException("Никнейм должен содержать от 3 до 16 латинских символов, цифр или знаков подчеркивания (_).");
            }

            return clean;
        }

        /// <summary>
        /// Генерирует детерминированный UUID v3 для оффлайн/пиратского игрока.
        /// Полностью совпадает с реализацией UUID.nameUUIDFromBytes в Java (Minecraft standard).
        /// </summary>
        public static PlayerProfile GetOfflineProfile(string nickname)
        {
            string validNick = ValidateNickname(nickname);

            // Mojang использует MD5("OfflinePlayer:" + nickname) с версией 3 и RFC 4122 вариантом
            byte[] bytes = Encoding.UTF8.GetBytes("OfflinePlayer:" + validNick);
            byte[] hash = MD5.HashData(bytes);

            // Устанавливаем версию 3
            hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
            // Устанавливаем RFC 4122 variant
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

            // Конвертируем в UUID строку без дефисов
            var guid = new Guid(hash);
            string uuidStr = guid.ToString("N");

            return new PlayerProfile
            {
                Nickname = validNick,
                Uuid = uuidStr,
                SkinUrl = "",
                IsOffline = true
            };
        }

        /// <summary>
        /// Запрашивает профиль игрока из ely.by Skinsystem API.
        /// Возвращает UUID и прямой URL скина.
        /// При отсутствии игрока или 204/404 выбрасывает InvalidOperationException.
        /// </summary>
        public static async Task<PlayerProfile> GetElyProfileAsync(string nickname)
        {
            string validNick = ValidateNickname(nickname);
            string encodedNick = Uri.EscapeDataString(validNick);
            string profileUrl = $"{Config.ElybySkinsystemBase}/profile/{encodedNick}";

            try
            {
                using var response = await HttpClient.GetAsync(profileUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent ||
                    response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException($"Игрок '{validNick}' не зарегистрирован на ely.by.");
                }

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidOperationException($"Игрок '{validNick}' не найден на ely.by.");
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? id = null;
                if (root.TryGetProperty("id", out var idProp))
                {
                    id = idProp.GetString();
                }

                if (string.IsNullOrEmpty(id))
                {
                    throw new InvalidOperationException($"ely.by не вернул UUID для '{validNick}'");
                }

                string cleanUuid = id.Replace("-", "");
                string skinUrl = $"{Config.ElybySkinsystemBase}/skins/{encodedNick}.png";

                return new PlayerProfile
                {
                    Nickname = validNick,
                    Uuid = cleanUuid,
                    SkinUrl = skinUrl,
                    IsOffline = false
                };
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"Ошибка подключения к ely.by: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Возвращает ссылку на скин игрока для аватара в интерфейсе (c ely.by или ccshead/minotar fallback).
        /// </summary>
        public static string GetAvatarUrl(string nickname, bool isPirate)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                nickname = "Player";
            }

            if (!isPirate)
            {
                return $"{Config.ElybySkinsystemBase}/skins/{Uri.EscapeDataString(nickname)}.png";
            }

            return $"https://minotar.net/avatar/{Uri.EscapeDataString(nickname)}/64.png";
        }
    }
}
