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
        private readonly ZabbixClient _zabbixClient = new();
        private const string TokenPlaceholder = "***********************";

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = ZabbixUrlTextBox.Text.Trim().TrimEnd('/'); //sicherstellen dass bei der URl keine doppelten Slahes sind

                if (string.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show(
                        "Bitte vorher die URL des Zabbix-Servers angeben",
                        "Fehler",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                var version = await _zabbixClient.GetVersionAsync(url, IgnoreCertificateErrorsCheckBox.IsChecked == true);

                MessageBox.Show(
                    $"Verbindung erfolgreich!" +
                    $"\n\nZabbix Version: {version}",
                    "Verbindung testen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Verbindung fehlgeschlagen:" +
                    $"\n\n{ex.Message}",
                    "Verbindung testen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        public ZabbixConfigWindow()
        {
            InitializeComponent();

            var config = _configService.Load();

            IgnoreCertificateErrorsCheckBox.IsChecked = config.IgnoreCertificateErrors;
            DarkModeCheckBox.IsChecked = config.UseDarkMode;
            PollIntervalSecondsTextBox.Text = config.PollIntervalSeconds.ToString();

            if (!string.IsNullOrWhiteSpace(config.ZabbixUrl))
                ZabbixUrlTextBox.Text = config.ZabbixUrl;

            if (!string.IsNullOrWhiteSpace(config.ZabbixDashboardUrl))
                ZabbixDashboardUrlTextBox.Text = config.ZabbixDashboardUrl;

            if (_credentialService.HasToken())
            {
                ApiTokenTextBox.Password = TokenPlaceholder;
            }
            // Theme initial anwenden (global)
            ZabbixTrayMonitor.Services.ThemeService.ApplyTheme(_configService.Load().UseDarkMode);
        }


        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PollIntervalSecondsTextBox.Text.Trim(), out var pollIntervalSeconds))
            {
                MessageBox.Show(
                    "Bitte beim Abfrageintervall eine gültige Zahl eingeben",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                return;
            }

            if (pollIntervalSeconds < 10)
            {
                MessageBox.Show(
                    "Das Abfrageintervall sollte mindestens 10 Sekunden betragen",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                return;
            }

            var zabbixUrl = ZabbixUrlTextBox.Text.Trim().TrimEnd('/');
            var dashboardUrl = ZabbixDashboardUrlTextBox.Text.Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(dashboardUrl))
            {
                dashboardUrl = zabbixUrl;
            }

            var existingConfig = _configService.Load(); // fürs Severity-mapping, damit die Werte nicht überschrieben werden weil wir sie (noch?) nicht anzeigen
            var config = new ZabbixConfig
            {
                ZabbixUrl = zabbixUrl,
                PollIntervalSeconds = pollIntervalSeconds,
                IgnoreCertificateErrors = IgnoreCertificateErrorsCheckBox.IsChecked == true,
                UseDarkMode = DarkModeCheckBox.IsChecked == true,
                WarningSeverityThreshold = existingConfig.WarningSeverityThreshold,
                ErrorSeverityThreshold = existingConfig.ErrorSeverityThreshold,
                ZabbixDashboardUrl = dashboardUrl,
            };
            var token = ApiTokenTextBox.Password.Trim();

            if (token != TokenPlaceholder)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    if (_credentialService.HasToken())
                    {
                        _credentialService.DeleteToken();
                    }
                }
                else
                {
                    _credentialService.SaveToken(token);
                }
            }

            _configService.Save(config);

            ZabbixTrayMonitor.Services.ThemeService.ApplyTheme(config.UseDarkMode);

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
