using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZabbixTrayMonitor.Services;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Views;

namespace ZabbixTrayMonitor
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DateTime _lastTrayLeftClick = DateTime.MinValue;
        private readonly CredentialService _credentialService = new();
        private readonly ZabbixClient _zabbixClient = new();
        private readonly ConfigService _configService = new();
        private readonly DispatcherTimer _refreshTimer = new();
        private List<ZabbixProblem> _currentProblems = new();
        private ProblemsWindow? _problemsWindow;
        private bool _isRefreshing = false;
        private bool _ignoreNextTrayLeftClick = false;

        public MainWindow()
        {
            InitializeComponent();

            Hide(); // Mainframe ausblenden

            _refreshTimer.Tick += async (_, _) => await RefreshProblemsAsync();

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
            var config = _configService.Load();
            var configWindow = new ZabbixConfigWindow();
            try
            {
                ZabbixTrayMonitor.Services.ThemeService.ApplyTheme(config.UseDarkMode);
            }
            catch { }

            var result = configWindow.ShowDialog();

            if (result == true)
            {
                StartRefreshTimer();
                _ = RefreshProblemsAsync();
                // Falls ProblemsWindow offen ist, aktualisiere Theme
                try
                {
                    config = _configService.Load();
                    // Theme global anwenden
                    ZabbixTrayMonitor.Services.ThemeService.ApplyTheme(config.UseDarkMode);
                }
                catch { }
            }
        }

        private async void TrayIcon_Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshProblemsAsync();
        }

        private void OpenZabbixDashboard()
        {
            var config = _configService.Load();

            var url = string.IsNullOrWhiteSpace(config.ZabbixDashboardUrl)
                ? config.ZabbixUrl
                : config.ZabbixDashboardUrl;

            if (string.IsNullOrWhiteSpace(url))
                return;

            // URL validieren, nur http/https zulassen
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        private void TrayIcon_RightMouseDown(object sender, RoutedEventArgs e)
        {
            // Wenn das Problemfenster offen ist schließe es und verhindere, dass das anschließende Linksklicken das Fenster sofort wieder öffnet
            if (_problemsWindow?.IsVisible == true)
            {
                _problemsWindow.Hide();
                _ignoreNextTrayLeftClick = true;

                try
                {
                    if (TrayIcon.ContextMenu is not null)
                    {
                        TrayIcon.ContextMenu.IsOpen = true;
                    }
                }
                catch { }
            }
        }

        private async void TrayIcon_ShowProblems_Click(object sender, RoutedEventArgs e)
        {
            if (_ignoreNextTrayLeftClick)
            {
                _ignoreNextTrayLeftClick = false;
                return;
            }
            // anti spam klick
            var now = DateTime.Now;
            if ((now - _lastTrayLeftClick).TotalMilliseconds < 800)
                return;
            _lastTrayLeftClick = now;
            var config = _configService.Load();

            await RefreshProblemsAsync();

            if (_problemsWindow == null)
            {
                _problemsWindow = new ProblemsWindow(
                    _currentProblems,
                    config.WarningSeverityThreshold,
                    config.ErrorSeverityThreshold,
                    async () =>
                    {
                        await RefreshProblemsAsync();
                        return _currentProblems;
                    },
                    () => OpenZabbixDashboard()
                );
            }

            // Sicherstellen, dass das ProblemsWindow beim Öffnen aktuelle Daten zeigt wegen cache Problemen
            try
            {
                _problemsWindow.UpdateProblems(_currentProblems);
            }
            catch {}

            if (_problemsWindow.IsVisible)
            {
                _problemsWindow.Hide();
                return;
            }

            // Fenster unten rechts
            _problemsWindow.Left = SystemParameters.WorkArea.Right - _problemsWindow.Width - 2;
            _problemsWindow.Top = SystemParameters.WorkArea.Bottom - _problemsWindow.Height - 2;
            _problemsWindow.Show();
            _problemsWindow.Activate();
        }

        private void TrayIcon_OpenZabbix_Click(object sender, RoutedEventArgs e)
        {
            OpenZabbixDashboard();
        }

        private void StartRefreshTimer()
        {
            var config = _configService.Load();

            _refreshTimer.Stop();
            _refreshTimer.Interval = TimeSpan.FromSeconds(config.PollIntervalSeconds);
            _refreshTimer.Start();
        }

        private async Task RefreshProblemsAsync()
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;

            // Loading-Indikator
            try
            {
                if (_problemsWindow?.IsVisible == true)
                {
                    _problemsWindow.SetLoading(true);
                }
            }
            catch {}

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
                _currentProblems = problems;
                var lastUpdated = DateTime.Now;
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
                    TrayIcon.ToolTipText =
                        $"Zabbix\n---------\nFEHLER: {errorCount}\nWARNUNGEN: {warningCount}\n---------\nAktualisiert am: {lastUpdated:HH:mm:ss}\nURL: {config.ZabbixUrl}";
                }
                else if (warningCount > 0)
                {
                    SetTrayIcon("tray-warning.ico");
                    TrayIcon.ToolTipText =
                        $"Zabbix\n---------\nWARNUNGEN: {warningCount}\n---------\nAktualisiert am: {lastUpdated:HH:mm:ss}\nURL: {config.ZabbixUrl}";
                }
                else
                {
                    SetTrayIcon("tray-ok.ico");
                    TrayIcon.ToolTipText =
                        $"Zabbix\n---------\nKEINE PROBLEME\n---------\nAktualisiert am: {lastUpdated:HH:mm:ss}\nURL: {config.ZabbixUrl}";
                }

                // Wenn das ProblemsWindow sichtbar ist, aktualisiere  Inhalt
                try
                {
                    if (_problemsWindow?.IsVisible == true)
                    {
                        _problemsWindow.UpdateProblems(_currentProblems);
                    }
                }
                catch {}
            }
            catch (Exception ex)
            {
                SetTrayIcon("tray-unknown.ico");
                var lastUpdated = DateTime.Now;
                var cfg = _configService.Load();
                TrayIcon.ToolTipText =
                    $"Zabbix\n---------\nFEHLER: {ex.Message}\n---------\nAktualisiert am: {lastUpdated:HH:mm:ss}\nURL: {cfg.ZabbixUrl}";
            }
            finally
            {
                _isRefreshing = false;

                try
                {
                    if (_problemsWindow?.IsVisible == true)
                    {
                        _problemsWindow.SetLoading(false);
                    }
                }
                catch {}
            }
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

        private void TrayIcon_Info_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var infoWindow = new Views.InfoWindow();
                infoWindow.ShowDialog();
            }
            catch
            {
                MessageBox.Show("Version: unbekannt", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}