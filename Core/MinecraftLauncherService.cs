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
            var loc = LocalizationManager.Instance;
            string cleanMinecraftDir = Path.GetFullPath(minecraftDir).TrimEnd('\\', '/');
            Directory.CreateDirectory(cleanMinecraftDir);

            // 0. КРИТИЧЕСКИЙ ФИКС ДЛЯ ЧИСТОЙ УСТАНОВКИ:
            // Инсталлятор NeoForge (как и Forge) требует обязательного наличия файла launcher_profiles.json
            // в целевой папке игры, иначе мгновенно падает с ошибкой:
            // "There is no minecraft launcher profile in ..., you need to run the launcher first!"
            string launcherProfilesPath = Path.Combine(cleanMinecraftDir, "launcher_profiles.json");
            if (!File.Exists(launcherProfilesPath) || new FileInfo(launcherProfilesPath).Length < 10)
            {
                File.WriteAllText(launcherProfilesPath, "{\n  \"profiles\": {\n    \"neoforge\": {\n      \"name\": \"neoforge\",\n      \"type\": \"custom\",\n      \"lastVersionId\": \"neoforge-" + Config.DefaultNeoForgeVersion + "\"\n    }\n  },\n  \"settings\": {},\n  \"version\": 3\n}");
            }

            var mcPath = new MinecraftPath(cleanMinecraftDir);
            var launcher = new CMLauncher(mcPath);

            // 1. Поиск уже установленной версии NeoForge в папке versions
            string versionsDir = Path.Combine(cleanMinecraftDir, "versions");
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
                    log(loc.Get("log_neoforge_found", existingVersion));

                    // Гарантируем целостность библиотек существующей версии
                    try
                    {
                        var neoObj = await launcher.GetVersionAsync(existingVersion);
                        if (neoObj != null)
                        {
                            await launcher.CheckAndDownloadAsync(neoObj);
                        }
                    }
                    catch (Exception ex)
                    {
                        log($"[NEOFORGE LIBS] {ex.Message}");
                    }

                    return existingVersion;
                }
            }

            // 2. Скачивание базовых файлов Minecraft 1.21.1 при необходимости
            log(loc.Get("log_mc_checking", Config.MinecraftVersion));
            try
            {
                var version = await launcher.GetVersionAsync(Config.MinecraftVersion);
                if (version != null)
                {
                    log(loc.Get("log_mc_downloading", Config.MinecraftVersion));
                    await launcher.CheckAndDownloadAsync(version);
                }
            }
            catch (Exception ex)
            {
                log($"[GAME] CmlLib: {ex.Message}");
            }

            // 3. Определение версии NeoForge: из pack.toml сборки либо актуальной стабильной
            string neoVersion = Config.DefaultNeoForgeVersion;
            try
            {
                string packTomlPath = Path.Combine(cleanMinecraftDir, "pack.toml");
                if (File.Exists(packTomlPath))
                {
                    string tomlText = await File.ReadAllTextAsync(packTomlPath, ct);
                    var match = Regex.Match(tomlText, @"neoforge\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        neoVersion = match.Groups[1].Value.Trim();
                    }
                }
                else
                {
                    neoVersion = await GetLatestNeoForgeVersionAsync(Config.MinecraftVersion);
                }
            }
            catch
            {
                neoVersion = Config.DefaultNeoForgeVersion;
            }

            log(loc.Get("log_loader_installing", Config.ModLoader, Config.MinecraftVersion));
            log(loc.Get("log_neoforge_target", neoVersion));

            string installerUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoVersion}/neoforge-{neoVersion}-installer.jar";

            // Выполняем установку во временной изолированной папке, как в оригинале на Python
            string tempDir = Path.Combine(Path.GetTempPath(), "neoforge_installer_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string installerJar = Path.Combine(tempDir, $"neoforge-{neoVersion}-installer.jar");

            try
            {
                log(loc.Get("log_neoforge_download", installerUrl));
                using (var resp = await HttpClient.GetAsync(installerUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    resp.EnsureSuccessStatusCode();
                    using var inStream = await resp.Content.ReadAsStreamAsync(ct);
                    using var outStream = new FileStream(installerJar, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                    await inStream.CopyToAsync(outStream, ct);
                }

                log(loc["log_neoforge_installing"]);

                string execJava = javaPath;
                if (execJava.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string partner = execJava.Substring(0, execJava.Length - 9) + "java.exe";
                    if (File.Exists(partner)) execJava = partner;
                }

                // Гарантируем launcher_profiles.json непосредственно перед вызовом инсталлятора
                if (!File.Exists(launcherProfilesPath) || new FileInfo(launcherProfilesPath).Length < 10)
                {
                    File.WriteAllText(launcherProfilesPath, "{\n  \"profiles\": {\n    \"neoforge\": {\n      \"name\": \"neoforge\",\n      \"type\": \"custom\",\n      \"lastVersionId\": \"neoforge-" + neoVersion + "\"\n    }\n  },\n  \"settings\": {},\n  \"version\": 3\n}");
                }

                var psi = new ProcessStartInfo
                {
                    FileName = execJava,
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-jar");
                psi.ArgumentList.Add(installerJar);
                psi.ArgumentList.Add("--install-client");
                psi.ArgumentList.Add(cleanMinecraftDir);

                var logLines = new List<string>();
                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                proc.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        logLines.Add(e.Data.TrimEnd());
                        log($"[NEOFORGE] {e.Data.TrimEnd()}");
                    }
                };
                proc.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        logLines.Add(e.Data.TrimEnd());
                        log($"[NEOFORGE] {e.Data.TrimEnd()}");
                    }
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                await proc.WaitForExitAsync(ct);

                if (proc.ExitCode != 0)
                {
                    string errDetails = string.Join("\n", logLines.TakeLast(5));
                    log($"[NEOFORGE ERROR] Exit code {proc.ExitCode}: {errDetails}");
                    throw new InvalidOperationException(loc.Get("err_neoforge_installer_exit", proc.ExitCode, errDetails));
                }
            }
            finally
            {
                // Очистка временной директории
                try
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch { }
            }

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
                    log(loc.Get("log_neoforge_success", installedId));

                    // Загружаем недостающие библиотеки для установленной версии NeoForge
                    try
                    {
                        log(loc.Get("log_neoforge_downloading_libs", installedId));
                        var neoObj = await launcher.GetVersionAsync(installedId);
                        if (neoObj != null)
                        {
                            await launcher.CheckAndDownloadAsync(neoObj);
                        }
                    }
                    catch (Exception ex)
                    {
                        log($"[NEOFORGE LIBS] {ex.Message}");
                    }

                    return installedId;
                }
            }

            throw new InvalidOperationException(loc["err_neoforge_not_found"]);
        }

        /// <summary>
        /// Очищает пользовательские аргументы JVM от параметров выделения памяти (-Xmx, -Xms).
        /// Защита от дурака: память контролируется строго через ползунок настроек.
        /// </summary>
        public static string SanitizeJvmArguments(string? args)
        {
            if (string.IsNullOrWhiteSpace(args))
                return string.Empty;

            // Удаляем любые флаги -Xmx и -Xms (например -Xmx4G, -Xms1024M, -Xmx=4096m)
            string clean = Regex.Replace(args, @"\s*-Xm[xs]\S*", "", RegexOptions.IgnoreCase);
            return clean.Trim();
        }

        /// <summary>
        /// Удаляет дубликаты JAR-библиотек из -cp (classpath) и -p (module-path).
        /// Решает критическую ошибку вылета NeoForge UnionFileSystem:
        /// java.lang.IllegalStateException: Duplicate key ... commons-lang3-3.14.0.jar
        /// </summary>
        public static string DeduplicateClasspath(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return arguments;

            var cpPatterns = new[] { "-cp ", "-classpath ", "-p ", "--module-path " };
            string result = arguments;

            foreach (var pattern in cpPatterns)
            {
                int index = result.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (index == -1) continue;

                int valueStart = index + pattern.Length;
                if (valueStart >= result.Length) continue;

                char quoteChar = '\0';
                if (result[valueStart] == '"' || result[valueStart] == '\'')
                {
                    quoteChar = result[valueStart];
                    valueStart++;
                }

                int valueEnd;
                if (quoteChar != '\0')
                {
                    valueEnd = result.IndexOf(quoteChar, valueStart);
                    if (valueEnd == -1) valueEnd = result.Length;
                }
                else
                {
                    valueEnd = result.IndexOf(' ', valueStart);
                    if (valueEnd == -1) valueEnd = result.Length;
                }

                string rawPath = result.Substring(valueStart, valueEnd - valueStart);
                var entries = rawPath.Split(';');

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cleanEntries = new List<string>();
                foreach (var entry in entries)
                {
                    string trimmed = entry.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                    {
                        cleanEntries.Add(trimmed);
                    }
                }

                string newPath = string.Join(";", cleanEntries);
                int replaceStart = valueStart;
                int replaceLength = valueEnd - valueStart;

                if (quoteChar != '\0')
                {
                    newPath = quoteChar + newPath + quoteChar;
                    replaceStart = valueStart - 1;
                    replaceLength = (valueEnd < result.Length && result[valueEnd] == quoteChar)
                        ? (valueEnd - replaceStart + 1)
                        : (valueEnd - replaceStart);
                }

                result = result.Substring(0, replaceStart) + newPath + result.Substring(replaceStart + replaceLength);
            }

            return result;
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
            string? customJvmArgs,
            Action<string> log,
            Action? onGameExited = null)
        {
            // Валидация объема выделяемой ОЗУ
            int safeRam = ramMb;
            if (safeRam < 1024 || safeRam > 65536)
            {
                safeRam = 4096;
            }

            // Формирование JVM аргументов:
            // 1. Память от ползунка (единственный параметр -Xmx)
            var jvmArgs = new List<string>
            {
                $"-Xmx{safeRam}M"
            };

            // 2. Пользовательские аргументы с защитой от дурака
            string sanitizedCustom = SanitizeJvmArguments(customJvmArgs);
            if (!string.IsNullOrWhiteSpace(sanitizedCustom))
            {
                var tokens = sanitizedCustom.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                jvmArgs.AddRange(tokens);
            }
            else
            {
                // Дефолтные флаги оптимизации G1GC
                jvmArgs.AddRange(new[]
                {
                    "-XX:+UseG1GC",
                    "-XX:+ParallelRefProcEnabled",
                    "-XX:MaxGCPauseMillis=200"
                });
            }

            // 3. Передача скина ely.by если задан
            if (!string.IsNullOrWhiteSpace(profile.SkinUrl) &&
                Regex.IsMatch(profile.SkinUrl, @"^https?://[a-zA-Z0-9.\-_/:]+$"))
            {
                jvmArgs.Add($"-Delyby.skinUrl={profile.SkinUrl}");
            }

            // 4. Обязательный флаг совместимости CustomSkinLoader (в Minecraft 1.21.1 / NeoForge
            // правило skin-manager-patch выбрасывает IllegalStateException без этого флага)
            if (!jvmArgs.Any(a => a.StartsWith("-Dcustomskinloader.ignorePatchFailure=", StringComparison.OrdinalIgnoreCase)))
            {
                jvmArgs.Add("-Dcustomskinloader.ignorePatchFailure=true");
            }

            string cleanMinecraftDir = Path.GetFullPath(minecraftDir).TrimEnd('\\', '/');
            var mcPath = new MinecraftPath(cleanMinecraftDir);
            var launcher = new CMLauncher(mcPath);

            // В CmlLib.Core конструктор MSession имеет порядок параметров:
            // MSession(string username, string accessToken, string uuid)
            // Передача uuid вторым параметром приводила к передаче "mock_token" в аргумент --uuid игрового процесса!
            string validUuid = !string.IsNullOrWhiteSpace(profile.Uuid)
                ? profile.Uuid.Trim()
                : Guid.NewGuid().ToString("N");

            var session = new MSession(profile.Nickname, "mock_token", validUuid);

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

            var loc = LocalizationManager.Instance;
            log(loc["log_mc_generating_cmd"]);
            var process = await launcher.CreateProcessAsync(launchOption);

            // КРИТИЧЕСКИЙ ФИКС: Очистка дубликатов библиотек из classpath (-cp и -p),
            // чтобы UnionFileSystem NeoForge не падал с DuplicateKeyException
            string originalArgs = process.StartInfo.Arguments;

            // Защита аргумента --uuid от некорректных токенов
            if (originalArgs.Contains("--uuid mock_token"))
            {
                originalArgs = originalArgs.Replace("--uuid mock_token", $"--uuid {validUuid}");
            }

            string cleanArgs = DeduplicateClasspath(originalArgs);
            if (!string.Equals(process.StartInfo.Arguments, cleanArgs, StringComparison.Ordinal))
            {
                process.StartInfo.Arguments = cleanArgs;
                log(loc["log_mc_verified"]);
            }

            process.StartInfo.WorkingDirectory = cleanMinecraftDir;
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
                log(loc.Get("log_mc_exited", process.ExitCode));
                onGameExited?.Invoke();
            };

            log(loc["log_mc_starting"]);
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
