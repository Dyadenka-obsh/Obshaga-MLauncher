using System;
using System.Windows;
using System.Windows.Media;

namespace LauncherC.Core
{
    public enum AppTheme
    {
        Modern,
        Retro
    }

    /// <summary>
    /// Менеджер тем оформления для LauncherC.
    /// Поддерживает:
    /// 1. Тема №1 — «Современная» (Aperture Modern 2.0 hi-tech: тёмный глубокий фон #101216, неон #FF9A00, скругления, Segoe UI).
    /// 2. Тема №2 — «Ретро» (Aperture 1973 Terminal в стиле версии 1.2: затемнённый тёплый шоколадно-янтарный фон #1E1208/#2C1B0D,
    ///    золото #D4AF37, моноширинный шрифт Consolas, плоские границы, большой ретро-баннер 1973 года и CRT-сканлайны).
    /// </summary>
    public static class ThemeManager
    {
        public static AppTheme CurrentTheme { get; private set; } = AppTheme.Modern;

        public static event Action<AppTheme>? ThemeChanged;

        /// <summary>
        /// Применяет тему (Modern или Retro) ко всем ресурсам приложения на лету.
        /// </summary>
        public static void ApplyTheme(AppTheme theme)
        {
            CurrentTheme = theme;
            var res = Application.Current.Resources;

            if (theme == AppTheme.Retro)
            {
                // ===== РЕТРО ТЕМА (1.2 / 1973 CRT TERMINAL) =====
                // Делаем фон терминала заметно темнее (#1E1208 / #2C1B0D), чтобы logo_1973.png
                // не сливался с фоном и выглядел контрастно и эффектно!
                res["BrushWindowBg"] = new SolidColorBrush(Color.FromRgb(0x1B, 0x10, 0x07));
                res["BrushCardBg"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x19, 0x0C));
                res["BrushConsoleBg"] = new SolidColorBrush(Color.FromRgb(0x14, 0x0B, 0x04));
                res["BrushBorder"] = new SolidColorBrush(Color.FromRgb(0x4A, 0x2E, 0x14));
                res["BrushWindowBorder"] = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)); // Aperture Retro Gold

                res["BrushAccent"] = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));
                res["BrushAccentHover"] = new SolidColorBrush(Color.FromRgb(0xE5, 0xC4, 0x58));
                res["BrushAccentPressed"] = new SolidColorBrush(Color.FromRgb(0xB8, 0x93, 0x26));
                res["BrushAccentText"] = new SolidColorBrush(Color.FromRgb(0x1B, 0x10, 0x07));

                res["BrushTextPrimary"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xEC, 0xDF));
                res["BrushTextSecondary"] = new SolidColorBrush(Color.FromRgb(0xC8, 0xB6, 0x9E));
                res["BrushTextAccent"] = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));

                // Кнопки обычной панели
                res["BrushBtnNormalBg"] = new SolidColorBrush(Color.FromRgb(0x34, 0x1F, 0x0E));
                res["BrushBtnNormalHover"] = new SolidColorBrush(Color.FromRgb(0x48, 0x2C, 0x15));
                res["BrushBtnNormalBorder"] = new SolidColorBrush(Color.FromRgb(0x56, 0x37, 0x18));

                // Кнопка PLAY в стиле ретро
                res["BrushPlayBtnBg"] = new SolidColorBrush(Color.FromRgb(0x3D, 0x25, 0x10));
                res["BrushPlayBtnText"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xEC, 0xDF));
                res["BrushPlayBtnBorder"] = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));

                // Шрифты: моноширинный Consolas для всего окна как в 1.2
                res["FontFamilyMain"] = new FontFamily("Consolas");
                res["FontFamilyMono"] = new FontFamily("Consolas");

                // Скругления: строгие плоские терминальные углы (0px)
                res["CornerRadiusWindow"] = new CornerRadius(0);
                res["CornerRadiusCard"] = new CornerRadius(0);
                res["CornerRadiusButton"] = new CornerRadius(0);

                // Видимость CRT-сканлайнов и ретро-баннера
                res["ScanlinesVisibility"] = Visibility.Visible;
                res["RetroBannerVisibility"] = Visibility.Visible;
                res["ThemeTitleBadge"] = "TERMINAL 1973 (v1.2)";
                res["LogoImageSource"] = "Assets/logo_1973.png";
            }
            else
            {
                // ===== СОВРЕМЕННАЯ ТЕМА (MODERN HI-TECH APERTURE 2.0) =====
                res["BrushWindowBg"] = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x16));
                res["BrushCardBg"] = new SolidColorBrush(Color.FromRgb(0x16, 0x18, 0x1F));
                res["BrushConsoleBg"] = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x14));
                res["BrushBorder"] = new SolidColorBrush(Color.FromRgb(0x25, 0x2A, 0x36));
                res["BrushWindowBorder"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x00)); // Amber

                res["BrushAccent"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x00));
                res["BrushAccentHover"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xAE, 0x33));
                res["BrushAccentPressed"] = new SolidColorBrush(Color.FromRgb(0xCC, 0x6E, 0x00));
                res["BrushAccentText"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x11, 0x15));

                res["BrushTextPrimary"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
                res["BrushTextSecondary"] = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                res["BrushTextAccent"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x00));

                res["BrushBtnNormalBg"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x26));
                res["BrushBtnNormalHover"] = new SolidColorBrush(Color.FromRgb(0x26, 0x2D, 0x3B));
                res["BrushBtnNormalBorder"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x3D));

                // Кнопка PLAY в современном стиле с градиентом
                var playGrad = new LinearGradientBrush(
                    Color.FromRgb(0xFF, 0x9A, 0x00),
                    Color.FromRgb(0xE6, 0x7E, 0x00),
                    new Point(0, 0), new Point(1, 1));
                res["BrushPlayBtnBg"] = playGrad;
                res["BrushPlayBtnText"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x11, 0x15));
                res["BrushPlayBtnBorder"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xAE, 0x33));

                // Шрифты
                res["FontFamilyMain"] = new FontFamily("Segoe UI");
                res["FontFamilyMono"] = new FontFamily("Consolas");

                // Скругления
                res["CornerRadiusWindow"] = new CornerRadius(0);
                res["CornerRadiusCard"] = new CornerRadius(10);
                res["CornerRadiusButton"] = new CornerRadius(6);

                // Сканлайны и ретро-баннер отключены в современной теме
                res["ScanlinesVisibility"] = Visibility.Collapsed;
                res["RetroBannerVisibility"] = Visibility.Collapsed;
                res["ThemeTitleBadge"] = "MODERN 2.0";
                res["LogoImageSource"] = "Assets/icon.ico";
            }

            ThemeChanged?.Invoke(CurrentTheme);
        }

        /// <summary>
        /// Переключает на противоположную тему (Modern <-> Retro).
        /// </summary>
        public static AppTheme ToggleTheme()
        {
            var target = CurrentTheme == AppTheme.Modern ? AppTheme.Retro : AppTheme.Modern;
            ApplyTheme(target);
            return target;
        }

        /// <summary>
        /// Парсит строковое название темы ("modern" или "retro").
        /// </summary>
        public static AppTheme Parse(string? name)
        {
            if (string.Equals(name, "retro", StringComparison.OrdinalIgnoreCase))
            {
                return AppTheme.Retro;
            }
            return AppTheme.Modern;
        }

        public static string ToStringName(AppTheme theme)
        {
            return theme == AppTheme.Retro ? "retro" : "modern";
        }
    }
}
