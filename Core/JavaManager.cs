using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LauncherC.Core
{
    /// <summary>
    /// Менеджер среды выполнения Java (JVM).
    /// Отвечает за поиск, валидацию версии (требуется Java 21 для Minecraft 1.21.1 / NeoForge)
    /// и автоматическую установку официального Adoptium Eclipse Temurin 21 OpenJDK при необходимости.
    /// Переписано с launcher/java_manager.py.
    /// </summary>
    public static class JavaManager
    {
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

        /// <summary>
        /// Проверяет наличие подходящей Java 21 и возвращает абсолютный путь к javaw.exe / java.exe.
        /// Если Java не найдена - скачивает и распаковывает официальный рантайм Java 21 в папку игры.
        /// </summary>
        public static async Task<string> EnsureJava21Async(
            string minecraftDir,
            string? customJavaPath,
            Action<string> log,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            var loc = LocalizationManager.Instance;

            // 1. Проверяем пользовательский путь, если указан
            if (!string.IsNullOrWhiteSpace(customJavaPath) && File.Exists(customJavaPath))
            {
                log(loc.Get("log_jvm_custom_checking", customJavaPath));
                if (CheckJavaVersion(customJavaPath) >= 21)
                {
                    log(loc.Get("log_jvm_custom_verified", customJavaPath));
                    return customJavaPath;
                }
                log(loc["log_jvm_custom_low"]);
            }

            // 2. Проверяем стандартный Mojang рантайм в папке игры
            string mojangJava1 = Path.Combine(minecraftDir, "runtime", Config.JvmRuntimeName, "windows-x64", Config.JvmRuntimeName, "bin", "javaw.exe");
            string mojangJava2 = Path.Combine(minecraftDir, "runtime", Config.JvmRuntimeName, "windows-x64", Config.JvmRuntimeName, "bin", "java.exe");

            if (File.Exists(mojangJava1)) return mojangJava1;
            if (File.Exists(mojangJava2)) return mojangJava2;

            // 3. Проверяем локальный рантайм Adoptium java-21 в папке игры
            string adoptiumDir = Path.Combine(minecraftDir, "runtime", "java-21");
            string? localExe = FindJavaExeInDir(adoptiumDir);
            if (!string.IsNullOrEmpty(localExe) && CheckJavaVersion(localExe) >= 21)
            {
                log(loc.Get("log_jvm_local_found", localExe));
                return localExe;
            }

            // 4. Проверяем системные пути (JAVA_HOME, Program Files)
            string? systemJava = FindSystemJava21();
            if (!string.IsNullOrEmpty(systemJava))
            {
                log(loc.Get("log_jvm_system_found", systemJava));
                return systemJava;
            }

            // 5. Автоматическая загрузка Java 21
            log(loc["log_jvm_not_found"]);
            log(loc["log_jvm_download_start"]);
            log(loc["log_jvm_download_size"]);

            return await DownloadAndInstallAdoptiumAsync(minecraftDir, log, progress, ct);
        }

        /// <summary>
        /// Скачивает официальный архив OpenJDK 21 от Adoptium и распаковывает в runtime/java-runtime-delta/windows-x64/java-runtime-delta.
        /// Полностью восстанавливает стандартную структуру рантайма Mojang/Python.
        /// </summary>
        private static async Task<string> DownloadAndInstallAdoptiumAsync(
            string minecraftDir,
            Action<string> log,
            IProgress<double>? progress,
            CancellationToken ct)
        {
            var loc = LocalizationManager.Instance;
            string runtimeRoot = Path.Combine(minecraftDir, "runtime");
            Directory.CreateDirectory(runtimeRoot);

            // Стандартный путь как в Python: runtime/java-runtime-delta/windows-x64/java-runtime-delta
            string targetDir = Path.Combine(runtimeRoot, Config.JvmRuntimeName, "windows-x64", Config.JvmRuntimeName);
            string zipPath = Path.Combine(runtimeRoot, "java-temp.zip");

            try
            {
                log(loc.Get("log_jvm_download_req", Config.AdoptiumJava21Url));

                using (var response = await HttpClient.GetAsync(Config.AdoptiumJava21Url, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;
                    using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                    using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                        totalRead += read;

                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            double percentage = (double)totalRead / totalBytes.Value * 100.0;
                            progress?.Report(percentage);
                        }
                    }
                }

                log(loc["log_jvm_extracting"]);

                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }
                Directory.CreateDirectory(targetDir);

                // Распаковываем во временную папку, так как архив содержит корневую папку вида jdk-21.0.x+y
                string extractTemp = Path.Combine(runtimeRoot, "extract_temp");
                if (Directory.Exists(extractTemp)) Directory.Delete(extractTemp, true);

                ZipFile.ExtractToDirectory(zipPath, extractTemp);

                // Перемещаем содержимое первого вложенного каталога в targetDir
                var subDirs = Directory.GetDirectories(extractTemp);
                string sourceDir = subDirs.Length == 1 ? subDirs[0] : extractTemp;

                foreach (var dir in Directory.GetDirectories(sourceDir))
                {
                    string dest = Path.Combine(targetDir, Path.GetFileName(dir));
                    Directory.Move(dir, dest);
                }
                foreach (var file in Directory.GetFiles(sourceDir))
                {
                    string dest = Path.Combine(targetDir, Path.GetFileName(file));
                    File.Move(file, dest);
                }

                Directory.Delete(extractTemp, true);
                if (File.Exists(zipPath)) File.Delete(zipPath);

                // Создаем .version файл для полной совместимости со структурой Mojang
                try
                {
                    string versionFile = Path.Combine(runtimeRoot, Config.JvmRuntimeName, "windows-x64", ".version");
                    File.WriteAllText(versionFile, "21.0.7\n");
                }
                catch { }

                // Удаляем старую нестандартную папку java-21 при ее наличии
                try
                {
                    string legacyJava21 = Path.Combine(runtimeRoot, "java-21");
                    if (Directory.Exists(legacyJava21)) Directory.Delete(legacyJava21, true);
                }
                catch { }

                string? javaExe = FindJavaExeInDir(targetDir);
                if (string.IsNullOrEmpty(javaExe))
                {
                    throw new FileNotFoundException(loc["err_java_not_found"]);
                }

                log(loc.Get("log_jvm_installed", javaExe));
                return javaExe;
            }
            catch (Exception ex)
            {
                if (File.Exists(zipPath))
                {
                    try { File.Delete(zipPath); } catch { }
                }
                throw new InvalidOperationException(loc.Get("err_java_download", ex.Message), ex);
            }
        }

        /// <summary>
        /// Ищет javaw.exe или java.exe в каталоге bin или рекурсивно.
        /// </summary>
        public static string? FindJavaExeInDir(string dir)
        {
            if (!Directory.Exists(dir)) return null;

            string directJavaw = Path.Combine(dir, "bin", "javaw.exe");
            if (File.Exists(directJavaw)) return directJavaw;

            string directJava = Path.Combine(dir, "bin", "java.exe");
            if (File.Exists(directJava)) return directJava;

            foreach (var file in Directory.GetFiles(dir, "javaw.exe", SearchOption.AllDirectories))
            {
                return file;
            }
            foreach (var file in Directory.GetFiles(dir, "java.exe", SearchOption.AllDirectories))
            {
                return file;
            }

            return null;
        }

        /// <summary>
        /// Проверяет наличие установленной Java 21+ в системе.
        /// </summary>
        private static string? FindSystemJava21()
        {
            // JAVA_HOME
            string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                string? exe = FindJavaExeInDir(javaHome);
                if (!string.IsNullOrEmpty(exe) && CheckJavaVersion(exe) >= 21)
                {
                    return exe;
                }
            }

            // Стандартные каталоги Windows
            string[] commonRoots = new[]
            {
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\Java",
                @"C:\Program Files\BellSoft",
                @"C:\Program Files\Microsoft"
            };

            foreach (var root in commonRoots)
            {
                if (!Directory.Exists(root)) continue;

                foreach (var dir in Directory.GetDirectories(root))
                {
                    string? exe = FindJavaExeInDir(dir);
                    if (!string.IsNullOrEmpty(exe) && CheckJavaVersion(exe) >= 21)
                    {
                        return exe;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Запускает 'java.exe -version' и парсит мажорный номер версии.
        /// </summary>
        public static int CheckJavaVersion(string javaExePath)
        {
            try
            {
                // Для javaw.exe лучше проверять парный java.exe, чтобы считать stdout/stderr консоли
                string checkExe = javaExePath.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase)
                    ? javaExePath.Substring(0, javaExePath.Length - 9) + "java.exe"
                    : javaExePath;

                if (!File.Exists(checkExe)) checkExe = javaExePath;

                var psi = new ProcessStartInfo
                {
                    FileName = checkExe,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return 0;

                string output = proc.StandardError.ReadToEnd() + "\n" + proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);

                // Поиск шаблона "21.x.x" или "1.8.x"
                var match = Regex.Match(output, @"version\s+""?(\d+)(\.(\d+))?");
                if (match.Success)
                {
                    int major = int.Parse(match.Groups[1].Value);
                    if (major == 1 && match.Groups[3].Success)
                    {
                        major = int.Parse(match.Groups[3].Value); // Для "1.8" -> 8
                    }
                    return major;
                }
            }
            catch
            {
                // Игнорируем ошибки запуска
            }

            return 0;
        }
    }
}
