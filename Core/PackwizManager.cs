using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace LauncherC.Core
{
    /// <summary>
    /// Менеджер синхронизации модов через официальный packwiz-installer-bootstrap.
    /// Переписано с launcher/packwiz.py.
    /// Гарантирует целостность bootstrap-файла по SHA-256 и выполняет автоматическое обновление
    /// клиентских модов, шейдеров и конфигов с GitHub-репозитория сервера.
    /// </summary>
    public static class PackwizManager
    {
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// Вычисляет хэш SHA-256 указанного файла.
        /// </summary>
        public static string ComputeSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            byte[] hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Скачивает и проверяет SHA-256 контрольную сумму packwiz-installer-bootstrap.jar.
        /// </summary>
        public static async Task EnsureBootstrapAsync(string minecraftDir, Action<string> log, CancellationToken ct = default)
        {
            var loc = LocalizationManager.Instance;
            string jarPath = Path.Combine(minecraftDir, "packwiz-installer-bootstrap.jar");
            string expectedHash = Config.PackwizBootstrapSha256.ToLowerInvariant();

            if (File.Exists(jarPath))
            {
                string actual = ComputeSha256(jarPath);
                if (string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Файл уже проверен и готов
                }

                log(loc["log_packwiz_hash_mismatch"]);
                try { File.Delete(jarPath); } catch { }
            }

            Directory.CreateDirectory(minecraftDir);
            string tempPath = jarPath + ".tmp";
            log(loc["log_packwiz_downloading"]);

            using (var request = new HttpRequestMessage(HttpMethod.Get, Config.PackwizBootstrapUrl))
            {
                request.Headers.UserAgent.ParseAdd("ApertureLauncher/1.3.0");
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                using var inStream = await response.Content.ReadAsStreamAsync(ct);
                using var outStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await inStream.CopyToAsync(outStream, ct);
            }

            string downloadedHash = ComputeSha256(tempPath);
            if (!string.Equals(downloadedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                throw new InvalidOperationException(
                    loc.Get("err_packwiz_integrity", expectedHash, downloadedHash)
                );
            }

            if (File.Exists(jarPath)) File.Delete(jarPath);
            File.Move(tempPath, jarPath);
            log(loc["log_packwiz_verified"]);
        }

        /// <summary>
        /// Запускает процесс синхронизации модов:
        /// java -jar packwiz-installer-bootstrap.jar -g -s client [PACKWIZ_PACK_TOML_URL]
        /// </summary>
        public static async Task SyncModpackAsync(
            string minecraftDir,
            string javaPath,
            Action<string> log,
            CancellationToken ct = default)
        {
            var loc = LocalizationManager.Instance;
            string cleanDir = Path.GetFullPath(minecraftDir).TrimEnd('\\', '/');
            await EnsureBootstrapAsync(cleanDir, log, ct);

            string jarPath = Path.Combine(cleanDir, "packwiz-installer-bootstrap.jar");

            // Использование парного java.exe для консольного вывода если передан javaw.exe
            string execJava = javaPath;
            if (execJava.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase))
            {
                string partner = execJava.Substring(0, execJava.Length - 9) + "java.exe";
                if (File.Exists(partner)) execJava = partner;
            }

            var psi = new ProcessStartInfo
            {
                FileName = execJava,
                WorkingDirectory = cleanDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-jar");
            psi.ArgumentList.Add(jarPath);
            psi.ArgumentList.Add("-g");
            psi.ArgumentList.Add("-s");
            psi.ArgumentList.Add("client");
            psi.ArgumentList.Add(Config.PackwizPackTomlUrl);

            log(loc.Get("log_packwiz_run_sync", $"{execJava} -jar {Path.GetFileName(jarPath)} -g -s client"));

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    log($"[PACKWIZ] {e.Data.TrimEnd()}");
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    log($"[PACKWIZ ERROR] {e.Data.TrimEnd()}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(loc.Get("err_packwiz_failed", process.ExitCode));
            }

            log(loc["log_packwiz_success"]);
        }

        /// <summary>
        /// Очищает папку mods в директории игры для принудительного полного перекачивания.
        /// </summary>
        public static void ClearModsDirectory(string minecraftDir)
        {
            string modsDir = Path.Combine(minecraftDir, "mods");
            if (Directory.Exists(modsDir))
            {
                Directory.Delete(modsDir, true);
            }
        }
    }
}
