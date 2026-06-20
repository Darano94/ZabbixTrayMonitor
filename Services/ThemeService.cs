using System.Windows;
using System.Windows.Media;

// Verwaltet die Farben basierend auf den Einstellungen (Dark/Light Mode)

namespace ZabbixTrayMonitor.Services
{
    public static class ThemeService
    {
        public static void ApplyTheme(bool darkMode)
        {
            var appResources = Application.Current.Resources;

            if (darkMode)
            {
                appResources["WindowBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16161A"));
                appResources["PrimaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8E8E8"));
                appResources["SecondaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9AA0A6"));
                appResources["DividerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
                appResources["LoadingBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8AB4F8"));
                appResources["InputBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F0F11"));
                appResources["InputBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
            }
            else
            {
                appResources["WindowBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                appResources["PrimaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"));
                appResources["SecondaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
                appResources["DividerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6E6E6"));
                appResources["LoadingBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A73E8"));
                appResources["InputBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                appResources["InputBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            }
        }
    }
}
