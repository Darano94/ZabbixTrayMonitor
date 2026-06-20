using System.Windows;

namespace ZabbixTrayMonitor.Views
{
    public partial class InfoWindow : Window
    {
        public InfoWindow()
        {
            InitializeComponent();

            try
            {
                var assembly = System.Reflection.Assembly.GetEntryAssembly();
                var version = assembly?.GetName().Version?.ToString() ?? "unbekannt";
                var location = assembly?.Location ?? string.Empty;
                string fileVersion = "unbekannt";
                try
                {
                    if (!string.IsNullOrWhiteSpace(location))
                    {
                        var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(location);
                        fileVersion = info.ProductVersion ?? info.FileVersion ?? version;
                    }
                }
                catch { }

                AppNameText.Text = "Zabbix Tray Monitor";
                VersionText.Text = $"Version: {fileVersion}";
                PathText.Text = string.IsNullOrWhiteSpace(location) ? "" : $"Programm: {location}";

                try
                {
                    var cfg = new ZabbixTrayMonitor.Services.ConfigService();
                    var cfgPath = cfg.GetConfigPath();
                    if (!string.IsNullOrWhiteSpace(cfgPath))
                    {
                        var cfgText = $"Configfile: {cfgPath}";
                        // add on a new line
                        PathText.Text += string.IsNullOrWhiteSpace(PathText.Text) ? cfgText : "\n" + cfgText;
                    }
                }
                catch { }
            }
            catch
            {
                AppNameText.Text = "Zabbix Tray Monitor";
                VersionText.Text = "Version: unbekannt";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
