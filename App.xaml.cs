using System.Windows;
using ZabbixTrayMonitor.Services;

namespace ZabbixTrayMonitor
{
    /// <summary>
    /// Interaktionslogik für App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Theme aus gespeicherter Konfiguration vor Fensterstart anwenden
            try
            {
                var configService = new ConfigService();
                if (configService.ConfigExists())
                {
                    var cfg = configService.Load();
                    ThemeService.ApplyTheme(cfg.UseDarkMode);
                }
            }
            catch
            {
                // Theme-Anwendungsfehler ignorieren
            }
        }
    }
}
