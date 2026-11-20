using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LauncherC.Core
{
    public record UpdateResult(bool HasUpdate, string? LatestVersion, string? ReleaseUrl, string? ReleaseNotes);

    /// <summary>
    /// Сервис проверки обновлений через GitHub Releases API.
    /// Переписано с launcher/updater.py.
    /// Выполняет корректное сравнение версий SemVer (например, 1.3.0 > 1.2.0).
    /// </summary>
    public static class UpdateChecker
    {
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(7) };

        static UpdateChecker()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"ApertureLauncher/{Config.CurrentVersion}");
            HttpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
        }

        public static async Task<UpdateResult> CheckForUpdatesAsync()
        {
            try
            {
                string url = $"https://api.github.com/repos/{Config.GitHubRepo}/releases/latest";
                string json = await HttpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
                string? htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
                string? notes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;

                if (string.IsNullOrWhiteSpace(tagName))
                {
                    return new UpdateResult(false, null, null, null);
                }

                string latestClean = tagName.TrimStart('v', 'V');
                string currentClean = Config.CurrentVersion.TrimStart('v', 'V');

                bool hasUpdate = IsNewer(latestClean, currentClean);
                return new UpdateResult(hasUpdate, tagName, htmlUrl, notes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UPDATE CHECK ERROR] {ex.Message}");
                return new UpdateResult(false, null, null, null);
            }
        }

        private static bool IsNewer(string latestStr, string currentStr)
        {
            if (Version.TryParse(latestStr, out var latest) && Version.TryParse(currentStr, out var current))
            {
                return latest > current;
            }

            // Fallback строковое сравнение
            return !string.Equals(latestStr, currentStr, StringComparison.OrdinalIgnoreCase);
        }
    }
}
