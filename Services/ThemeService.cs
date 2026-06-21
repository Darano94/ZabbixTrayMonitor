using System.Windows;
using System.Windows.Media;

// Verwaltet die Farben anhand der Dark-/Lighmode Option in den Einstellungen
// aktualisiert App.xaml Ressourcen mit den entsprechenden Farben, damit die UI die neuen Farben übernimmt (durch DynamicResource Bindings in XAML)

namespace ZabbixTrayMonitor.Services
{
    public static class ThemeService
    {
        public static void ApplyTheme(bool darkMode)
        {
            var appResources = Application.Current.Resources; // Zugriff auf die Ressourcen von App.xaml

            void SetBrush(string key, string hex)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);

                if (appResources[key] is SolidColorBrush existingBrush)
                {
                    if (existingBrush.IsFrozen)
                    {
                        // Gefrorene Brush ersetzen weil sie nicht mehr geändert werden kann
                        appResources[key] = new SolidColorBrush(color);
                    }
                    else
                    {
                        existingBrush.Color = color;
                    }
                }
                else
                {
                    appResources[key] = new SolidColorBrush(color);
                }
            }

            void SetColor(string key, string hex)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                appResources[key] = color;
            }

            if (darkMode)
            {
                SetBrush("WindowBackgroundBrush", "#16161A");
                SetBrush("PrimaryTextBrush", "#E8E8E8");
                SetBrush("SecondaryTextBrush", "#9AA0A6");
                SetBrush("DividerBrush", "#4A4A50");
                SetBrush("LoadingBrush", "#8AB4F8");
                SetBrush("InputBackgroundBrush", "#0F0F11");
                SetBrush("InputBorderBrush", "#2A2A2A");
                SetBrush("ReadOnlyInputBackgroundBrush", "#242428");
                SetBrush("HoverBackgroundBrush", "#1F1F1F");

                SetColor("HoverShadowColor", "#000000");
            }
            else
            {
                SetBrush("WindowBackgroundBrush", "#FFFFFF");
                SetBrush("PrimaryTextBrush", "#1A1A1A");
                SetBrush("SecondaryTextBrush", "#666666");
                SetBrush("DividerBrush", "#D0D0D0");
                SetBrush("LoadingBrush", "#1A73E8");
                SetBrush("InputBackgroundBrush", "#FFFFFF");
                SetBrush("InputBorderBrush", "#CCCCCC");
                SetBrush("ReadOnlyInputBackgroundBrush", "#EEEEEE");
                SetBrush("HoverBackgroundBrush", "#F2F2F2");

                SetColor("HoverShadowColor", "#000000");
            }
        }
    }
}