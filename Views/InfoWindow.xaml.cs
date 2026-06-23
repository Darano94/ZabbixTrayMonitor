using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using ZabbixTrayMonitor.Services;

namespace ZabbixTrayMonitor.Views
{
    public partial class InfoWindow : Window
    {
        public InfoWindow()
        {
            InitializeComponent();

            try
            {
                var configService = new ConfigService();
                var config = configService.Load();

                var appName = string.IsNullOrWhiteSpace(config.AppName)
                    ? "ZabbixTrayMonitor"
                    : config.AppName;

                var assembly = Assembly.GetEntryAssembly(); // Assembly ist die gebaute exe
                var assemblyVersion = assembly?.GetName().Version?.ToString() ?? "unbekannt";
                var location = Environment.ProcessPath ?? AppContext.BaseDirectory;
                var fileVersion = GetFileVersion(location, assemblyVersion);
                var configPath = configService.GetConfigPath();

                Title = $"{appName} - Info";
                HeaderTitleInfo.Text = $"{appName} - Info";

                AppNameText.Text = appName;
                VersionText.Text = $"Version: {fileVersion}";
                ProgramText.Text = string.IsNullOrWhiteSpace(location)
                    ? string.Empty
                    : $"Programm: {location}";

                ConfigText.Text = string.IsNullOrWhiteSpace(configPath)
                    ? string.Empty
                    : $"Config: {configPath}";
            }
            catch
            {
                AppNameText.Text = "ZabbixTrayMonitor";
                VersionText.Text = "Version: unbekannt";
                ProgramText.Text = string.Empty;
                ConfigText.Text = string.Empty;
            }
        }

        private static string GetFileVersion(string location, string fallbackVersion)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(location))
                    return fallbackVersion;

                // versucht die Produktversion zu bekommen sonst die Dateiversion sonst die Assembly-Version
                var info = FileVersionInfo.GetVersionInfo(location);

                return info.ProductVersion
                    ?? info.FileVersion
                    ?? fallbackVersion;
            }
            catch
            {
                return fallbackVersion;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                    DragMove();
            }
            catch { }
        }
    }
}
