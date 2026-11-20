using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LauncherC.Core;
using LauncherC.UI;

namespace LauncherC
{
    /// <summary>
    /// Главное окно лаунчера Aperture Science LauncherC для проекта OBSHAGA.
    /// Переписано с Python / Tkinter (launcher/gui/app.py) на C# / WPF.
    /// Поддерживает тему Modern 2.0 и Retro 1973 (в стиле v1.2), перетаскивание окна,
    /// синхронизацию модов packwiz и запуск Minecraft 1.21.1 NeoForge.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly LauncherSettings _settings;
        private readonly LocalizationManager _loc = LocalizationManager.Instance;
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

        private string? _packChangelog;
        private string? _launcherChangelog;
        private string _currentTab = "pack";
        private bool _isGameRunning = false;

        public MainWindow()
        {
            InitializeComponent();

            // 1. Загрузка сохранённых настроек пользователя
            _settings = SettingsManager.LoadSettings();

            // 2. Инициализация языка
            _loc.SetLanguage(_settings.Language);
            _loc.LanguageChanged += OnLanguageChanged;

            // 3. Подписка на смену темы
            ThemeManager.ThemeChanged += OnThemeChanged;

            // 4. Применение сохранённой темы и начальных значений UI
            var currentTheme = ThemeManager.Parse(_settings.Theme);
            ThemeManager.ApplyTheme(currentTheme);

            ApplyLocalization();
            UpdateThemeDisplay(currentTheme);
            InitUserProfile();

            // 5. Инициализация Discord RPC в неблокирующем фоновом потоке
            Task.Run(() => DiscordRpcManager.Instance.Initialize());

            // 6. Фоновая проверка обновлений с GitHub (версия 2.0.0)
            Task.Run(CheckForUpdatesAsync);

            // 7. Загрузка новостей и чейнджлогов
            Task.Run(LoadChangelogsAsync);

            Log(_loc["log_sys_init"]);
            Log(_loc.Get("log_sys_version", Config.CurrentVersion, "2.0.0", ".NET 10 WPF"));
            Log(_loc.Get("log_sys_theme", currentTheme == AppTheme.Retro ? _loc["theme_opt_retro"] : _loc["theme_opt_modern"]));
            Log(_loc.Get("log_sys_game_dir", SettingsManager.GetActiveMinecraftPath(_settings)));
        }

        #region Перетаскивание и управление окном

        /// <summary>
        /// Позволяет свободно перетаскивать окно по экрану при нажатии на любую неинтерактивную область окна.
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                var source = e.OriginalSource as DependencyObject;
                while (source != null && source != this)
                {
                    if (source is Button || source is TextBox || source is ProgressBar || 
                        source is ScrollBar || source is CheckBox || source is Slider)
                    {
                        return; // Не перетаскивать при клике по интерактивным элементам
                    }
                    source = VisualTreeHelper.GetParent(source);
                }

                try
                {
                    DragMove();
                }
                catch { }
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    DragMove();
                }
                catch { }
            }
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentNickname();
            DiscordRpcManager.Instance.Dispose();
            Application.Current.Shutdown();
        }

        #endregion

        #region Управление темами (Modern / Retro 1973)

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var nextTheme = ThemeManager.ToggleTheme();
            _settings.Theme = ThemeManager.ToStringName(nextTheme);
            SettingsManager.SaveSettings(_settings);

            UpdateThemeDisplay(nextTheme);
            Log(_loc.Get("log_theme_switched", nextTheme == AppTheme.Retro ? _loc["theme_opt_retro"] : _loc["theme_opt_modern"]));
        }

        private void OnThemeChanged(AppTheme newTheme)
        {
            Dispatcher.Invoke(() => UpdateThemeDisplay(newTheme));
        }

        private void UpdateThemeDisplay(AppTheme theme)
        {
            if (theme == AppTheme.Retro)
            {
                BtnThemeToggle.Content = _loc["theme_btn_retro"];
                TxtThemeBadge.Text = "TERMINAL 1973 (v1.2)";
            }
            else
            {
                BtnThemeToggle.Content = _loc["theme_btn_modern"];
                TxtThemeBadge.Text = "MODERN 2.0";
            }

            UpdateNewsDisplay();
        }

        #endregion

        #region Локализация

        private void OnLanguageChanged()
        {
            Dispatcher.Invoke(ApplyLocalization);
        }

        private void ApplyLocalization()
        {
            LblProjectBadge.Text = _loc["header_project"];
            LblNickname.Text = _loc["nickname"];
            BtnLang.Content = "🌐 " + _loc.CurrentLanguage.ToUpperInvariant();
            BtnSettings.Content = "⚙ " + _loc["settings_title"];

            BtnTabPack.Content = "📦 " + _loc["tab_pack"];
            BtnTabLauncher.Content = "🚀 " + _loc["tab_launcher"];

            ChkAutoScroll.Content = _loc["log_autoscroll"];
            BtnClearLog.Content = _loc["log_clear"];

            UpdateAuthModeDisplay();
            UpdateThemeDisplay(ThemeManager.CurrentTheme);

            if (!_isGameRunning && BtnPlay.IsEnabled)
            {
                BtnPlay.Content = _loc["play"];
            }

            UpdateNewsDisplay();
        }

        private void BtnLang_Click(object sender, RoutedEventArgs e)
        {
            string next = _loc.ToggleNextLanguage();
            _settings.Language = next;
            SettingsManager.SaveSettings(_settings);
            Log(_loc.Get("log_lang_switched", next.ToUpperInvariant()));
        }

        #endregion

        #region Профиль игрока и ely.by / пиратский режим

        private void InitUserProfile()
        {
            TxtNickname.Text = !string.IsNullOrWhiteSpace(_settings.LastNickname) ? _settings.LastNickname : "Dyadenka";
            TxtRamHint.Text = $"{_settings.RamMb} MB";
            UpdateAuthModeDisplay();
            UpdateAvatarPreview();
        }

        private void SaveCurrentNickname()
        {
            string nick = TxtNickname.Text.Trim();
            if (!string.IsNullOrEmpty(nick))
            {
                _settings.LastNickname = nick;
                SettingsManager.SaveSettings(_settings);
            }
        }

        private void TxtNickname_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveCurrentNickname();
            UpdateAvatarPreview();
        }

        private void TxtNickname_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SaveCurrentNickname();
                UpdateAvatarPreview();
                Keyboard.ClearFocus();
            }
        }

        private void BtnAuthToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsPirate = !_settings.IsPirate;
            SettingsManager.SaveSettings(_settings);
            UpdateAuthModeDisplay();
            UpdateAvatarPreview();
        }

        private void UpdateAuthModeDisplay()
        {
            if (_settings.IsPirate)
            {
                TxtAuthIcon.Text = "⚪";
                TxtAuthMode.Text = _loc["auth_pirate"];
                TxtAuthMode.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            }
            else
            {
                TxtAuthIcon.Text = "🔶";
                TxtAuthMode.Text = _loc["auth_ely"];
                TxtAuthMode.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextAccent");
            }
        }

        private void UpdateAvatarPreview()
        {
            string nick = TxtNickname.Text.Trim();
            if (string.IsNullOrWhiteSpace(nick)) nick = "Dyadenka";

            try
            {
                string avatarUrl = AuthService.GetAvatarUrl(nick, _settings.IsPirate);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(avatarUrl, UriKind.Absolute);
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DownloadCompleted += (_, _) =>
                {
                    Dispatcher.Invoke(() => ImgAvatar.Source = bitmap);
                };
                bitmap.DownloadFailed += (_, _) =>
                {
                    Dispatcher.Invoke(() => ImgAvatar.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/icon.ico")));
                };
                bitmap.EndInit();
            }
            catch
            {
                ImgAvatar.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/icon.ico"));
            }
        }

        #endregion

        #region Новости и чейнджлоги

        private async Task LoadChangelogsAsync()
        {
            try
            {
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                _packChangelog = await _http.GetStringAsync(Config.PackChangelogUrl);
            }
            catch { }

            try
            {
                _launcherChangelog = await _http.GetStringAsync(Config.LauncherChangelogUrl);
            }
            catch { }

            Dispatcher.Invoke(UpdateNewsDisplay);
        }

        private void UpdateNewsDisplay()
        {
            string fallback = _loc["news_error"];
            if (_currentTab == "pack")
            {
                TxtNews.Text = !string.IsNullOrWhiteSpace(_packChangelog) ? _packChangelog : fallback;
                BtnTabPack.SetResourceReference(Button.BackgroundProperty, "BrushAccent");
                BtnTabPack.SetResourceReference(Button.ForegroundProperty, "BrushAccentText");
                BtnTabLauncher.SetResourceReference(Button.BackgroundProperty, "BrushBtnNormalBg");
                BtnTabLauncher.SetResourceReference(Button.ForegroundProperty, "BrushTextPrimary");
            }
            else
            {
                TxtNews.Text = !string.IsNullOrWhiteSpace(_launcherChangelog) ? _launcherChangelog : fallback;
                BtnTabPack.SetResourceReference(Button.BackgroundProperty, "BrushBtnNormalBg");
                BtnTabPack.SetResourceReference(Button.ForegroundProperty, "BrushTextPrimary");
                BtnTabLauncher.SetResourceReference(Button.BackgroundProperty, "BrushAccent");
                BtnTabLauncher.SetResourceReference(Button.ForegroundProperty, "BrushAccentText");
            }
        }

        private void BtnTabPack_Click(object sender, RoutedEventArgs e)
        {
            _currentTab = "pack";
            UpdateNewsDisplay();
        }

        private void BtnTabLauncher_Click(object sender, RoutedEventArgs e)
        {
            _currentTab = "launcher";
            UpdateNewsDisplay();
        }

        private void BtnRefreshNews_Click(object sender, RoutedEventArgs e)
        {
            TxtNews.Text = _loc["news_loading"];
            Task.Run(LoadChangelogsAsync);
        }

        #endregion

        #region Быстрые действия и Настройки

        private void BtnDiscord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(Config.DiscordInviteUrl) { UseShellExecute = true });
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWin = new SettingsWindow(_settings)
            {
                Owner = this
            };
            settingsWin.SettingsUpdated += () =>
            {
                TxtRamHint.Text = $"{_settings.RamMb} MB";
                Log(_loc.Get("log_settings_updated", SettingsManager.GetActiveMinecraftPath(_settings)));
            };
            settingsWin.ShowDialog();
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TxtConsoleLog.Clear();
        }

        #endregion

        #region Логирование

        public void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                TxtConsoleLog.AppendText($"[{timestamp}] {message}\n");

                if (ChkAutoScroll.IsChecked == true)
                {
                    TxtConsoleLog.ScrollToEnd();
                }
            });
        }

        private void SetStatus(string text, Color dotColor)
        {
            Dispatcher.Invoke(() =>
            {
                LblStatusText.Text = $"[{text.ToUpperInvariant()}]";
                LblStatusText.Foreground = new SolidColorBrush(dotColor);
                StatusDot.Fill = new SolidColorBrush(dotColor);
            });
        }

        #endregion

        #region Проверка обновлений (2.0.0)

        private async Task CheckForUpdatesAsync()
        {
            var result = await UpdateChecker.CheckForUpdatesAsync();
            if (result.HasUpdate && !string.IsNullOrEmpty(result.ReleaseUrl))
            {
                Dispatcher.Invoke(() =>
                {
                    string msg = _loc.Get("msg_update_available", result.LatestVersion ?? Config.CurrentVersion, Config.CurrentVersion);
                    var ans = MessageBox.Show(this, msg, _loc["title_update_available"], MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (ans == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(result.ReleaseUrl) { UseShellExecute = true });
                    }
                });
            }
        }

        #endregion

        #region Пайплайн запуска игры (Game Pipeline)

        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_isGameRunning)
            {
                MessageBox.Show(this, _loc["msg_game_running"], _loc["title_warning"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveCurrentNickname();
            string rawNick = TxtNickname.Text.Trim();

            try
            {
                AuthService.ValidateNickname(rawNick);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _loc["title_input_error"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnPlay.IsEnabled = false;
            BtnPlay.Content = _loc["loading"];
            LaunchProgressBar.Visibility = Visibility.Visible;
            LaunchProgressBar.IsIndeterminate = true;

            SetStatus(_loc["status_syncing"], Color.FromRgb(255, 154, 0));

            await Task.Run(() => ExecuteLaunchPipeline(rawNick));
        }

        private async Task ExecuteLaunchPipeline(string nickname)
        {
            try
            {
                Log(_loc["log_sys_launch_init"]);

                // 1. Определение активной директории Minecraft
                string mcDir = SettingsManager.GetActiveMinecraftPath(_settings);
                Log(_loc.Get("log_game_dir", mcDir));

                // 2. Аутентификация игрока
                PlayerProfile profile;
                if (_settings.IsPirate)
                {
                    Log(_loc.Get("log_auth_pirate", nickname));
                    profile = AuthService.GetOfflineProfile(nickname);
                    Log(_loc.Get("log_auth_uuid", profile.Uuid));
                }
                else
                {
                    Log(_loc.Get("log_ely_request", nickname));
                    try
                    {
                        profile = await AuthService.GetElyProfileAsync(nickname);
                        Log(_loc.Get("log_ely_authorized", profile.Uuid));
                    }
                    catch (Exception ex)
                    {
                        Log($"[ELY ERROR] {ex.Message}");

                        bool switchToPirate = false;
                        Dispatcher.Invoke(() =>
                        {
                            var res = MessageBox.Show(this,
                                _loc.Get("msg_ely_fallback_prompt", nickname, ex.Message),
                                _loc["title_ely_unavailable"],
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            switchToPirate = (res == MessageBoxResult.Yes);
                        });

                        if (switchToPirate)
                        {
                            _settings.IsPirate = true;
                            SettingsManager.SaveSettings(_settings);
                            Dispatcher.Invoke(UpdateAuthModeDisplay);

                            profile = AuthService.GetOfflineProfile(nickname);
                            Log(_loc.Get("log_auth_fallback_offline", profile.Uuid));
                        }
                        else
                        {
                            throw new OperationCanceledException(_loc["msg_launch_cancelled"]);
                        }
                    }
                }

                // 3. Проверка и подготовка Java 21
                SetStatus(_loc["status_downloading_java"], Color.FromRgb(255, 154, 0));
                Log(_loc["log_jvm_checking"]);

                var javaProgress = new Progress<double>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        LaunchProgressBar.IsIndeterminate = false;
                        LaunchProgressBar.Value = p;
                    });
                });

                string javaPath = await JavaManager.EnsureJava21Async(
                    mcDir,
                    _settings.CustomJavaPath,
                    Log,
                    javaProgress
                );

                Dispatcher.Invoke(() => LaunchProgressBar.IsIndeterminate = true);

                // 4. Синхронизация модпака через packwiz
                SetStatus(_loc["status_syncing"], Color.FromRgb(255, 154, 0));
                Log(_loc["log_packwiz_syncing"]);

                if (_settings.ForceUpdate)
                {
                    Log(_loc["log_packwiz_force_clean"]);
                    PackwizManager.ClearModsDirectory(mcDir);
                    _settings.ForceUpdate = false;
                    SettingsManager.SaveSettings(_settings);
                }

                await PackwizManager.SyncModpackAsync(mcDir, javaPath, Log);

                // 5. Установка Minecraft 1.21.1 и NeoForge
                SetStatus(_loc["status_installing_game"], Color.FromRgb(255, 154, 0));
                string versionId = await MinecraftLauncherService.EnsureGameAndNeoForgeInstalledAsync(
                    mcDir,
                    javaPath,
                    Log
                );

                // 6. Запуск игрового клиента
                Log(_loc.Get("log_game_transfer", versionId));
                DiscordRpcManager.Instance.UpdateStatus("В игре на OBSHAGA", $"Ник: {nickname}");

                int ram = _settings.RamMb > 0 ? _settings.RamMb : 4096;
                Log(_loc.Get("log_game_ram", ram));

                _isGameRunning = true;
                SetStatus(_loc["status_playing"], Color.FromRgb(0, 162, 255));

                Dispatcher.Invoke(() =>
                {
                    BtnPlay.Content = _loc["btn_in_game"];
                    BtnPlay.IsEnabled = false;
                    LaunchProgressBar.Visibility = Visibility.Collapsed;
                });

                await MinecraftLauncherService.LaunchGameAsync(
                    versionId,
                    mcDir,
                    javaPath,
                    profile,
                    ram,
                    _settings.JvmArguments,
                    Log,
                    onGameExited: () =>
                    {
                        _isGameRunning = false;
                        Dispatcher.Invoke(() =>
                        {
                            BtnPlay.IsEnabled = true;
                            BtnPlay.Content = _loc["play"];
                            SetStatus(_loc["status_ready"], Color.FromRgb(34, 197, 94));
                        });
                        DiscordRpcManager.Instance.UpdateStatus(_loc["status_in_launcher"], "Aperture Science Development Center");
                    }
                );

                Log(_loc["log_sys_launch_success"]);
            }
            catch (OperationCanceledException)
            {
                Log(_loc["log_sys_launch_cancelled"]);
            }
            catch (Exception ex)
            {
                Log(_loc.Get("log_error_launch", ex.Message));
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(this, $"{_loc["error_launch_prefix"]}\n{ex.Message}", _loc["error_launch_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                if (!_isGameRunning)
                {
                    Dispatcher.Invoke(() =>
                    {
                        BtnPlay.IsEnabled = true;
                        BtnPlay.Content = _loc["play"];
                        LaunchProgressBar.Visibility = Visibility.Collapsed;
                        SetStatus(_loc["status_ready"], Color.FromRgb(34, 197, 94));
                    });
                }
            }
        }

        #endregion
    }
}
