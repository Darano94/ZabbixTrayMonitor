using System.Windows;
using System.Windows.Media.Imaging;
using ZabbixTrayMonitor.Services;
using System.Windows.Threading;

namespace ZabbixTrayMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly CredentialService _credentialService = new();
        private readonly ZabbixClient _zabbixClient = new();
        private readonly ConfigService _configService = new();
        private readonly DispatcherTimer _refreshTimer = new();
        private bool _isRefreshing = false;

        public MainWindow()
        {
            InitializeComponent();

            Hide(); // Mainframe ausblenden

            if (!_configService.ConfigExists())
            {
                var configWindow = new ZabbixConfigWindow();
                configWindow.ShowDialog();
            }

            StartRefreshTimer();
            _ = RefreshProblemsAsync();

        }
        private void TrayIcon_Settings_Click(object sender, RoutedEventArgs e)
        {
            var configWindow = new ZabbixConfigWindow();
            configWindow.ShowDialog();
        }

        private async void TrayIcon_Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshProblemsAsync();
        }

        private void StartRefreshTimer()
        {
            var config = _configService.Load();

            _refreshTimer.Interval = TimeSpan.FromSeconds(config.PollIntervalSeconds);
            _refreshTimer.Tick += async (_, _) => await RefreshProblemsAsync();
            _refreshTimer.Start();
        }

        private async Task RefreshProblemsAsync()
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;

            try
            {
                TrayIcon.ToolTipText = "Zabbix: Aktualisiere...";

                var config = _configService.Load();
                var token = _credentialService.GetToken();

                if (string.IsNullOrWhiteSpace(config.ZabbixUrl))
                    throw new Exception("Keine Zabbix URL konfiguriert");

                if (string.IsNullOrWhiteSpace(token))
                    throw new Exception("Kein API Token gespeichert");

                var problems = await _zabbixClient.GetProblemsAsync(
                    config.ZabbixUrl,
                    token,
                    config.IgnoreCertificateErrors
                );

                // zähle alle Probleme mit einer Severity >= unserem Error Schwellenwert
                var errorCount = problems.Count(p => p.Severity >= config.ErrorSeverityThreshold); 
              
                // zähle alle Probleme die mindestens unserem Warnungs Schwellenwert entsprechen aber noch kein Error sind
                var warningCount = problems.Count(p =>
                    p.Severity >= config.WarningSeverityThreshold &&
                    p.Severity < config.ErrorSeverityThreshold
                );

                if (errorCount > 0)
                {
                    SetTrayIcon("tray-error.ico");
                    TrayIcon.ToolTipText = $"Zabbix: {FormatCount(errorCount, "Fehler", "Fehler")}, {FormatCount(warningCount, "Warnung", "Warnungen")}";
                }
                else if (warningCount > 0)
                {
                    SetTrayIcon("tray-warning.ico");
                    TrayIcon.ToolTipText = $"Zabbix: {FormatCount(warningCount, "Warnung", "Warnungen")}";
                }
                else
                {
                    SetTrayIcon("tray-ok.ico");
                    TrayIcon.ToolTipText = "Zabbix: Keine Probleme";
                }
            }
            catch (Exception ex)
            {
                SetTrayIcon("tray-unknown.ico");
                TrayIcon.ToolTipText = $"Zabbix: Fehler - {ex.Message}";
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private static string FormatCount(int count, string singular, string plural)
        {
            return count == 1
                ? $"{count} {singular}"
                : $"{count} {plural}";
        }

        private void SetTrayIcon(string iconFileName)
        {
            TrayIcon.IconSource = new BitmapImage(
                new Uri($"pack://application:,,,/Assets/{iconFileName}")
            );
        }

        private void TrayIcon_Exit_Click(object sender, RoutedEventArgs e)
        {
            TrayIcon.Dispose();
            Application.Current.Shutdown();
        }
    }
}