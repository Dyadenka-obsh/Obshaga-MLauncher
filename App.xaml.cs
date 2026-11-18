using System.Windows;
using LauncherC.Core;

namespace LauncherC;

/// <summary>
/// Точка входа приложения. Инициализирует тему оформления (Современная или Ретро) до отображения окон.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Загружаем сохранённую тему из настроек
        var settings = SettingsManager.LoadSettings();
        var theme = ThemeManager.Parse(settings.Theme);
        ThemeManager.ApplyTheme(theme);
    }
}
