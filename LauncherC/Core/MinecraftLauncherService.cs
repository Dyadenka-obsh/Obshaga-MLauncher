using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Version;

namespace LauncherC.Core
{
    /// <summary>
    /// Сервис управления версиями, установкой NeoForge и запуском игры Minecraft.
    /// Переписано с launcher/game_launcher.py с использованием открытой библиотеки CmlLib.Core.
    /// 
    /// Реализует:
    /// 1. Автоматический поиск установленной версии NeoForge для Minecraft 1.21.1.
    /// 2. Установку ванильного клиента 1.21.1 и запуск официального инсталлятора NeoForge при первом запуске.
    /// 3. Формирование безопасных аргументов JVM (G1GC, память, скин ely.by) и прямое подключение к серверу.
    /// 4. Перенаправление логов игры в реальном времени с фильтрацией ANSI escape-последовательностей.
    /// </summary>
    public static class MinecraftLauncherService
    {
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };
        private static readonly Regex AnsiRegex = new(@"\x1B\[[0-9;]*[mK]", RegexOptions.Compiled);

        /// <summary>
        /// Проверяет наличие установленной версии NeoForge.
        /// Если не установлена — скачивает Minecraft 1.21.1 и устанавливает NeoForge через официальный installer JAR.
        /// Возвращает идентификатор установленной версии (например, 'neoforge-21.1.267').
        /// </summary>
        public static async Task<string> EnsureGameAndNeoForgeInstalledAsync(
            string minecraftDir,
            string javaPath,
            Action<string> log,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            var mcPath = new MinecraftPath(minecraftDir);
            var launcher = new CMLauncher(mcPath);

            // 1. Поиск уже установленной версии NeoForge в папке versions
            string versionsDir = Path.Combine(minecraftDir, "versions");
            if (Directory.Exists(versionsDir))
            {
                var neoforgeDirs = Directory.GetDirectories(versionsDir)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(name => name)
                    .ToList();

                if (neoforgeDirs.Any())
                {
                    string existingVersion = neoforgeDirs.Last()!;
                    log($"[GAME] Найдена установленная версия NeoForge: {existingVersion}");
                    return existingVersion;
                }
            }

            // 2. Скачивание базовых файлов Minecraft 1.21.1 при необходимости
            log($"[GAME] Базовая версия Minecraft {Config.MinecraftVersion} проверяется...");
            try
            {
                var version = await launcher.GetVersionAsync(Config.MinecraftVersion);
                if (version != null)
                {
                    log($"[GAME] Загрузка ресурсов для Minecraft {Config.MinecraftVersion}...");
                    await launcher.CheckAndDownloadAsync(version);
                }
            }
            catch (Exception ex)
            {
                log($"[GAME] Проверка ресурсов через CmlLib: {ex.Message}");
            }

            // 3. Установка NeoForge через официальный headless installer
            log($"[GAME] Установка модлоадера {Config.ModLoader} для {Config.MinecraftVersion}...");
            string neoVersion = await GetLatestNeoForgeVersionAsync(Config.MinecraftVersion);
            log($"[GAME] Целевая версия NeoForge: {neoVersion}");

            string installerUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoVersion}/neoforge-{neoVersion}-installer.jar";
            string installerJar = Path.Combine(minecraftDir, $"neoforge-{neoVersion}-installer.jar");

            log($"[GAME] Скачивание инсталлятора NeoForge: {installerUrl}...");
            using (var resp = await HttpClient.GetAsync(installerUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                using var inStream = await resp.Content.ReadAsStreamAsync(ct);
                using var outStream = new FileStream(installerJar, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await inStream.CopyToAsync(outStream, ct);
            }

            log("[GAME] Запуск установки NeoForge в фоновом режиме (это может занять 1-2 минуты)...");

            string execJava = javaPath;
            if (execJava.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase))
            {
                string partner = execJava.Substring(0, execJava.Length - 9) + "java.exe";
                if (File.Exists(partner)) execJava = partner;
            }

            var psi = new ProcessStartInfo
            {
                FileName = execJava,
                Arguments = $"-jar \"{installerJar}\" --installClient \"{minecraftDir}\"",
                WorkingDirectory = minecraftDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) log($"[NEOFORGE] {e.Data.TrimEnd()}");
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) log($"[NEOFORGE] {e.Data.TrimEnd()}");
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync(ct);

            // Удаляем временный файл инсталлятора
            try { File.Delete(installerJar); } catch { }

            // Повторно проверяем установленную версию
            if (Directory.Exists(versionsDir))
            {
                var neoforgeDirs = Directory.GetDirectories(versionsDir)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(name => name)
                    .ToList();

                if (neoforgeDirs.Any())
                {
                    string installedId = neoforgeDirs.Last()!;
                    log($"[GAME] NeoForge успешно установлен: {installedId}");
                    return installedId;
                }
            }

            throw new InvalidOperationException("Не удалось обнаружить установленный NeoForge после завершения работы инсталлятора.");
        }

        /// <summary>
        /// Формирует параметры запуска и запускает процесс Minecraft.
        /// </summary>
        public static async Task<Process> LaunchGameAsync(
            string versionId,
            string minecraftDir,
            string javaPath,
            PlayerProfile profile,
            int ramMb,
            Action<string> log,
            Action? onGameExited = null)
        {
            // Валидация объема выделяемой ОЗУ
            int safeRam = ramMb;
            if (safeRam < 1024 || safeRam > 65536)
            {
                safeRam = 4096;
            }

            // Формирование JVM аргументов
            var jvmArgs = new List<string>
            {
                $"-Xmx{safeRam}M",
                "-XX:+UseG1GC",
                "-XX:+ParallelRefProcEnabled",
                "-XX:MaxGCPauseMillis=200"
            };

            // Передача скина ely.by если задан
            if (!string.IsNullOrWhiteSpace(profile.SkinUrl) &&
                Regex.IsMatch(profile.SkinUrl, @"^https?://[a-zA-Z0-9.\-_/:]+$"))
            {
                jvmArgs.Add($"-Delyby.skinUrl={profile.SkinUrl}");
            }

            var mcPath = new MinecraftPath(minecraftDir);
            var launcher = new CMLauncher(mcPath);

            var session = new MSession(profile.Nickname, profile.Uuid, "mock_token");

            var launchOption = new MLaunchOption
            {
                Path = mcPath,
                StartVersion = await launcher.GetVersionAsync(versionId),
                Session = session,
                JavaPath = javaPath,
                MaximumRamMb = safeRam,
                JVMArguments = jvmArgs.ToArray(),
                ServerIp = Config.ServerHost,
                ServerPort = Config.ServerPort,
                GameLauncherName = "ApertureLauncherC",
                GameLauncherVersion = Config.CurrentVersion
            };

            log("[GAME] Генерация команды запуска игрового клиента...");
            var process = await launcher.CreateProcessAsync(launchOption);

            process.StartInfo.WorkingDirectory = minecraftDir;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    string clean = AnsiRegex.Replace(e.Data, "");
                    log($"[MC] {clean.TrimEnd()}");
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    string clean = AnsiRegex.Replace(e.Data, "");
                    log($"[MC] {clean.TrimEnd()}");
                }
            };

            process.Exited += (_, _) =>
            {
                log($"[SYS] Процесс Minecraft завершил работу (код выхода: {process.ExitCode}).");
                onGameExited?.Invoke();
            };

            log("[GAME] Запуск процесса Minecraft...");
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
        }

        /// <summary>
        /// Получает последнюю стабильную версию NeoForge для указанной версии Minecraft из официального Maven-репозитория.
        /// </summary>
        private static async Task<string> GetLatestNeoForgeVersionAsync(string mcVersion)
        {
            string fallback = "21.1.267";
            try
            {
                string metadataUrl = "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
                string xml = await HttpClient.GetStringAsync(metadataUrl);
                var doc = XDocument.Parse(xml);

                // Ищем версии вида 21.1.x
                string prefix = mcVersion.StartsWith("1.") ? mcVersion.Substring(2) : mcVersion; // 21.1
                var versions = doc.Descendants("version")
                    .Select(v => v.Value)
                    .Where(v => v.StartsWith(prefix + "."))
                    .ToList();

                if (versions.Any())
                {
                    return versions.Last();
                }
            }
            catch
            {
                // Fallback к известной стабильной версии
            }
            return fallback;
        }
    }
}
