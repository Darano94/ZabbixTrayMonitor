using System.Windows;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Services;

// Einstellungs-WPF-Fenster

namespace ZabbixTrayMonitor
{
    /// <summary>
    /// Interaktionslogik für ZabbixConfigWindow.xaml
    /// </summary>
    public partial class ZabbixConfigWindow : Window
    {
        private readonly ConfigService _configService = new();
        private readonly CredentialService _credentialService = new();
        private readonly string tokenPlaceholder = "***********************";

        public ZabbixConfigWindow()
        {
            InitializeComponent();

            var config = _configService.Load();

            if (!string.IsNullOrWhiteSpace(config.ZabbixUrl))
                ZabbixUrlTextBox.Text = config.ZabbixUrl;

            if (_credentialService.HasToken())
            {
                ApiTokenTextBox.Password = tokenPlaceholder;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var config = new ZabbixConfig
            {
                ZabbixUrl = ZabbixUrlTextBox.Text.Trim(),
                PollIntervalSeconds = 60
            };

            var token = ApiTokenTextBox.Password.Trim();

            if (!string.IsNullOrWhiteSpace(token) && token != tokenPlaceholder)
            {
                _credentialService.SaveToken(token);
            }

            _configService.Save(config);

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
