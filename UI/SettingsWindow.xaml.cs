using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LauncherC.Core;

namespace LauncherC.UI
{
    /// <summary>
    /// Логика взаимодействия для современного окна настроек SettingsWindow.xaml.
    /// Предоставляет выбор папки установки Minecraft, темы оформления, выделение ОЗУ и указание Java.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly LauncherSettings _settings;
        private readonly LocalizationManager _loc = LocalizationManager.Instance;
        private int _safeMaxRamMb = 8192;

        public event Action? SettingsUpdated;

        public SettingsWindow(LauncherSettings settings)
        {
            InitializeComponent();
            _settings = settings;

            CalculateSystemRam();
            ApplyLocalization();
            LoadCurrentValues();
            UpdateThemeButtonStates(ThemeManager.CurrentTheme);
            ApplySliderStyle(ThemeManager.CurrentTheme);
        }

        #region Перетаскивание окна

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySliderStyle(ThemeManager.CurrentTheme);
        }

        private void ApplySliderStyle(AppTheme theme)
        {
            string key = theme == AppTheme.Modern ? "ModernSliderStyle" : "RetroSliderStyle";
            if (Application.Current?.TryFindResource(key) is Style appStyle)
            {
                SliderRam.Style = appStyle;
            }
            else if (TryFindResource(key) is Style winStyle)
            {
                SliderRam.Style = winStyle;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                var source = e.OriginalSource as DependencyObject;
                while (source != null && source != this)
                {
                    if (source is Button || source is TextBox || source is ScrollBar || source is Slider)
                        return;
                    source = VisualTreeHelper.GetParent(source);
                }

                try { DragMove(); } catch { }
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Управление темами в настройках

        private void BtnThemeModern_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ApplyTheme(AppTheme.Modern);
            _settings.Theme = "modern";
            UpdateThemeButtonStates(AppTheme.Modern);
            ApplySliderStyle(AppTheme.Modern);
        }

        private void BtnThemeRetro_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ApplyTheme(AppTheme.Retro);
            _settings.Theme = "retro";
            UpdateThemeButtonStates(AppTheme.Retro);
            ApplySliderStyle(AppTheme.Retro);
        }

        private void UpdateThemeButtonStates(AppTheme active)
        {
            if (active == AppTheme.Modern)
            {
                BtnThemeModern.SetResourceReference(Button.BackgroundProperty, "BrushAccent");
                BtnThemeModern.SetResourceReference(Button.ForegroundProperty, "BrushAccentText");
                BtnThemeRetro.SetResourceReference(Button.BackgroundProperty, "BrushBtnNormalBg");
                BtnThemeRetro.SetResourceReference(Button.ForegroundProperty, "BrushTextPrimary");
                SliderRam.Style = (Style)FindResource("ModernSliderStyle");
            }
            else
            {
                BtnThemeModern.SetResourceReference(Button.BackgroundProperty, "BrushBtnNormalBg");
                BtnThemeModern.SetResourceReference(Button.ForegroundProperty, "BrushTextPrimary");
                BtnThemeRetro.SetResourceReference(Button.BackgroundProperty, "BrushAccent");
                BtnThemeRetro.SetResourceReference(Button.ForegroundProperty, "BrushAccentText");
                SliderRam.Style = (Style)FindResource("RetroSliderStyle");
            }
        }

        #endregion

        private void ApplyLocalization()
        {
            TxtTitle.Text = _loc["settings_title"];
            LblTheme.Text = _loc["theme_label"];
            BtnThemeModern.Content = _loc["theme_opt_modern"];
            BtnThemeRetro.Content = _loc["theme_opt_retro"];

            LblGameDir.Text = _loc["game_dir_label"];
            BtnBrowsePath.Content = _loc["game_dir_browse"];
            BtnDefaultPath.Content = _loc["game_dir_default"];
            LblGameDirHint.Text = _loc["game_dir_hint"];

            LblRamAlloc.Text = _loc["ram_alloc"];
            LblMaxSafeRam.Text = _loc.Get("max_safe_ram", _safeMaxRamMb);

            LblCustomJava.Text = _loc["custom_java_label"];
            BtnBrowseJava.Content = _loc["custom_java_browse"];

            LblJvmArgs.Text = _loc["jvm_args_label"];
            BtnDefaultJvmArgs.Content = _loc["jvm_args_default"];
            LblJvmArgsHint.Text = _loc["jvm_args_hint"];

            BtnOpenGameDir.Content = "📁 " + _loc["open_game_dir"];
            BtnOpenMods.Content = "📦 " + _loc["open_mods"];
            BtnRepairBuild.Content = "🛠 " + _loc["repair_build"];
            BtnJoinDiscord.Content = "💬 " + _loc["join_discord"];
            BtnSaveClose.Content = _loc["save_close"];
        }

        private void LoadCurrentValues()
        {
            // Путь к папке Minecraft
            string currentPath = !string.IsNullOrWhiteSpace(_settings.CustomMinecraftPath)
                ? _settings.CustomMinecraftPath
                : SettingsManager.GetDefaultMinecraftPath();

            TxtMinecraftPath.Text = currentPath;

            // Выделение ОЗУ
            SliderRam.Maximum = _safeMaxRamMb;
            int savedRam = _settings.RamMb > 0 ? _settings.RamMb : 4096;
            if (savedRam > _safeMaxRamMb) savedRam = _safeMaxRamMb;
            SliderRam.Value = savedRam;
            UpdateRamDisplay(savedRam);

            // Путь к Java
            TxtCustomJava.Text = _settings.CustomJavaPath ?? "";

            // Аргументы запуска JVM
            TxtJvmArgs.Text = !string.IsNullOrWhiteSpace(_settings.JvmArguments)
                ? _settings.JvmArguments
                : Config.DefaultJvmArguments;
        }

        private void CalculateSystemRam()
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    long totalMb = (long)(memStatus.ullTotalPhys / (1024 * 1024));
                    long safe = totalMb - 2048;
                    safe = (safe / 1024) * 1024;
                    _safeMaxRamMb = (int)Math.Max(2048, safe);
                    return;
                }
            }
            catch { }

            _safeMaxRamMb = 8192;
        }

        private void SliderRam_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int mb = (int)Math.Round(e.NewValue / 512.0) * 512;
            UpdateRamDisplay(mb);
        }

        private void UpdateRamDisplay(int mb)
        {
            if (TxtRamValue == null) return;
            double gb = mb / 1024.0;
            TxtRamValue.Text = $"{mb} МБ ({gb:0.0} ГБ)";
        }

        private void BtnBrowsePath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Выберите папку для установки сборки Minecraft",
                    InitialDirectory = Directory.Exists(TxtMinecraftPath.Text) ? TxtMinecraftPath.Text : SettingsManager.GetDefaultMinecraftPath()
                };

                if (dlg.ShowDialog(this) == true)
                {
                    TxtMinecraftPath.Text = dlg.FolderName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка открытия диалога выбора папки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDefaultPath_Click(object sender, RoutedEventArgs e)
        {
            TxtMinecraftPath.Text = SettingsManager.GetDefaultMinecraftPath();
        }

        private void BtnBrowseJava_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите javaw.exe или java.exe",
                Filter = "Java Executable (javaw.exe;java.exe)|javaw.exe;java.exe|Все файлы (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            if (dlg.ShowDialog(this) == true)
            {
                TxtCustomJava.Text = dlg.FileName;
            }
        }

        private void BtnOpenGameDir_Click(object sender, RoutedEventArgs e)
        {
            string dir = TxtMinecraftPath.Text.Trim();
            if (string.IsNullOrEmpty(dir)) dir = SettingsManager.GetDefaultMinecraftPath();

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }

        private void BtnOpenMods_Click(object sender, RoutedEventArgs e)
        {
            string dir = TxtMinecraftPath.Text.Trim();
            if (string.IsNullOrEmpty(dir)) dir = SettingsManager.GetDefaultMinecraftPath();

            string mods = Path.Combine(dir, "mods");
            if (!Directory.Exists(mods)) Directory.CreateDirectory(mods);
            Process.Start(new ProcessStartInfo("explorer.exe", mods) { UseShellExecute = true });
        }

        private void BtnRepairBuild_Click(object sender, RoutedEventArgs e)
        {
            string dir = TxtMinecraftPath.Text.Trim();
            if (string.IsNullOrEmpty(dir)) dir = SettingsManager.GetDefaultMinecraftPath();

            try
            {
                PackwizManager.ClearModsDirectory(dir);
                _settings.ForceUpdate = true;
                SettingsManager.SaveSettings(_settings);

                MessageBox.Show(this, _loc["msg_repair_info"], _loc["settings_title"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка очистки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnJoinDiscord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(Config.DiscordInviteUrl) { UseShellExecute = true });
        }

        private void BtnDefaultJvmArgs_Click(object sender, RoutedEventArgs e)
        {
            TxtJvmArgs.Text = Config.DefaultJvmArguments;
        }

        private void TxtJvmArgs_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateJvmArgs();
        }

        private bool ValidateJvmArgs()
        {
            string text = TxtJvmArgs.Text ?? string.Empty;
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"-Xm[xs]\S*", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                MessageBox.Show(this, _loc["jvm_args_memory_warning"], _loc["settings_title"], MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtJvmArgs.Text = MinecraftLauncherService.SanitizeJvmArguments(text);
                return false;
            }
            return true;
        }

        private void BtnSaveClose_Click(object sender, RoutedEventArgs e)
        {
            string newPath = TxtMinecraftPath.Text.Trim();
            string defaultPath = SettingsManager.GetDefaultMinecraftPath();

            if (string.Equals(newPath, defaultPath, StringComparison.OrdinalIgnoreCase))
            {
                _settings.CustomMinecraftPath = "";
            }
            else
            {
                _settings.CustomMinecraftPath = newPath;
            }

            _settings.RamMb = (int)SliderRam.Value;
            _settings.CustomJavaPath = TxtCustomJava.Text.Trim();

            // Защита от дурака: санитизация JVM аргументов
            ValidateJvmArgs();
            _settings.JvmArguments = MinecraftLauncherService.SanitizeJvmArguments(TxtJvmArgs.Text);

            _settings.Theme = ThemeManager.ToStringName(ThemeManager.CurrentTheme);

            SettingsManager.SaveSettings(_settings);
            SettingsUpdated?.Invoke();

            Close();
        }

        #region Win32 API
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
        #endregion
    }
}
