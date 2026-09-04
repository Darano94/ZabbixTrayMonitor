using System.Diagnostics;
using System.Linq;
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
        private readonly ZabbixClient _zabbixClient = new();
        private readonly ConfigService _configService = new();
        private readonly DispatcherTimer _refreshTimer = new();
        private readonly DispatcherTimer _trayToolTipDelayTimer = new();
        private List<ZabbixProblem> _currentProblems = new();
        private ProblemsWindow? _problemsWindow;
        private string? _lastRefreshError;
        private bool _isRefreshing = false;
        private bool _ignoreNextTrayLeftClick = false;
        private bool _trayToolTipOpenRequested = false;

        public MainWindow()
        {
            InitializeComponent();

            Hide(); // Mainframe ausblenden

            // Der Custom-Tooltip wird direkt vom TaskbarIcon gesteuert. Dadurch stammen
            // Öffnen und Schließen vom tatsächlichen Windows-Tray-Icon statt aus einer
            // eigenen Cursor-Abstandslogik.
            UpdateTrayToolTip("Zabbix Tray Monitor\n---------\nInitialisiere...");

            // RefreshTimer für Probleme initialisieren abhängig vom Pollintervall in den Einstellungen
            _refreshTimer.Tick += async (_, _) => await RefreshProblemsAsync();

            // Der Windows-Tray meldet zuverlässig, wann der Mauszeiger das Icon betritt bzw. verlässt.
            // Die eigentliche Anzeige des Custom-Tooltips verzögern wir zusätzlich konfigurierbar.
            _trayToolTipDelayTimer.Tick += TrayToolTipDelayTimer_Tick;

            if (!_configService.ConfigExists())
            {
                var configWindow = new ZabbixConfigWindow();
                configWindow.ShowDialog();
            }

            // Set main window title to include configured AppName after possible initial configuration
            try
            {
                var cfg = _configService.Load();
                this.Title = string.IsNullOrWhiteSpace(cfg.AppName) ? "Zabbix Tray Monitor" : $"{cfg.AppName} - Zabbix Tray Monitor";
            }
            catch { }

            // RefreshTimer starten 
            StartRefreshTimer();
            _ = RefreshProblemsAsync();
        }

        // öffnet Einstellungsfenster und aktualisiert nach Schließen/Speichern die App
        private void TrayIcon_Settings_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();

            // Wenn bereits ein Einstellungsfenster offen ist, aktiviere es statt ein neues zu öffnen
            var existingConfigWin = Application.Current.Windows.OfType<ZabbixConfigWindow>().FirstOrDefault();
            if (existingConfigWin != null)
            {
                existingConfigWin.Activate();
                return;
            }

            var config = _configService.Load();
            var configWindow = new ZabbixConfigWindow();

            try
            {
                ZabbixTrayMonitor.Services.ThemeService.ApplyTheme(config.UseDarkMode);
            }
            catch { }

            var result = configWindow.ShowDialog(); // 

            if (result == true)
            {
                // aktualisiert nach Speichern die Probleme sofort und startet Timer neu der Daten im Hintergrund aktualisiert
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
            HideTrayToolTip();
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

            // URL validieren lässt nur http und https zu
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return;

            Process.Start(new ProcessStartInfo // öffnet die URL mit dem Standardbrowser
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }

        private void TrayIcon_RightMouseDown(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();

            // Wenn Problemfenster offen ist wieder schließen und verhindern,
            // dass ein anschließender Linksklick das Fenster sofort wieder öffnet
            if (_problemsWindow?.IsVisible == true)
            {
                _problemsWindow.Hide();
                _ignoreNextTrayLeftClick = true;
            }

            try
            {
                if (TrayIcon.ContextMenu is not null)
                {
                    TrayIcon.ContextMenu.IsOpen = true;
                }
            }
            catch { }
        }

        private async void TrayIcon_ShowProblems_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();

            // Wenn das Problemfenster gerade mit Rechtsklick geschlossen wurde verhindern dass anschließender Linksklick es sofort wieder öffnet
            if (_ignoreNextTrayLeftClick)
            {
                _ignoreNextTrayLeftClick = false;
                return;
            }

            // anti spam Klick
            var now = DateTime.Now;
            if ((now - _lastTrayLeftClick).TotalMilliseconds < 800)
                return;

            _lastTrayLeftClick = now;
            var config = _configService.Load();

            await RefreshProblemsAsync();

            // Wenn der letzte Refresh fehlgeschlagen ist, keine alten Problem-Daten anzeigen
            if (!string.IsNullOrWhiteSpace(_lastRefreshError))
            {
                _problemsWindow?.Hide();
                return;
            }

            // Singleton für ProblemsWindow
            // Fenster aktualisiert sich immer mit den aktuellen Daten wenn es geöffnet wird damit keine Cacheprobleme 
            if (_problemsWindow == null)
            {
                _problemsWindow = new ProblemsWindow(
                    _currentProblems,
                    async () =>
                    {
                        await RefreshProblemsAsync();
                        return _currentProblems;
                    },
                    () => OpenZabbixDashboard(),
                    async (eventId, suppressUntil, message) =>
                    {
                        await ExecuteProblemActionAsync(eventId, suppressUntil, message);
                    },
                    _configService
                );
            }

            // Sicherstellen, dass das ProblemsWindow beim Öffnen aktuelle Daten zeigt wegen Cacheproblemen
            try
            {
                _problemsWindow.UpdateProblems(_currentProblems);
            }
            catch { }

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

        private void StartRefreshTimer()
        {
            var config = _configService.Load();

            _refreshTimer.Stop();
            _refreshTimer.Interval = TimeSpan.FromSeconds(config.PollIntervalSeconds);
            _refreshTimer.Start();
        }

        private async Task ExecuteProblemActionAsync(string eventId, DateTime? suppressUntil, string? messageOverride)
        {
            var config = _configService.Load();
            var credSuffix = string.IsNullOrWhiteSpace(config.CredentialTargetSuffix)
                ? "ApiToken"
                : config.CredentialTargetSuffix;

            var token = CredentialService.GetTokenForApp(config.AppName, credSuffix);

            if (string.IsNullOrWhiteSpace(config.ZabbixUrl))
                throw new Exception("Keine Zabbix URL konfiguriert");

            if (string.IsNullOrWhiteSpace(token))
                throw new Exception("Kein API Token gespeichert");

            var defaultMessage =
                ZabbixConfig.ResolveAcknowledgeMessage(config.AcknowledgeMessage);

            var message = string.IsNullOrWhiteSpace(messageOverride)
                ? defaultMessage
                : messageOverride.Trim();

            await _zabbixClient.AcknowledgeProblemAsync(
                config.ZabbixUrl,
                config.ZabbixApiEndpoint,
                token,
                config.IgnoreCertificateErrors,
                eventId,
                suppressUntil,
                message
            );
        }

        private async Task RefreshProblemsAsync()
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;

            // Loading Indikator
            try
            {
                if (_problemsWindow?.IsVisible == true)
                {
                    _problemsWindow.SetLoading(true);
                }
            }
            catch { }

            try
            {
                var config = _configService.Load();
                var credSuffix = string.IsNullOrWhiteSpace(config.CredentialTargetSuffix) ? "ApiToken" : config.CredentialTargetSuffix;
                var token = CredentialService.GetTokenForApp(config.AppName, credSuffix);

                if (string.IsNullOrWhiteSpace(config.ZabbixUrl))
                    throw new Exception("Keine Zabbix URL konfiguriert");

                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new Exception("Kein API Token gespeichert");
                }

                var problems = await _zabbixClient.GetProblemsAsync(
                    config.ZabbixUrl,
                    config.ZabbixApiEndpoint,
                    token,
                    config.WarningSeverityThreshold,
                    config.IgnoreCertificateErrors
                );

                // Unterdrückte Probleme zentral filtern, damit Tray-Status, Tooltip und
                // Problemfenster immer mit exakt derselben Problemmenge arbeiten.
                // Bestätigte, aber nicht unterdrückte Probleme bleiben weiterhin sichtbar.
                var visibleProblems = config.ShowSuppressedProblems
                    ? problems
                    : problems.Where(p => !p.Suppressed).ToList();

                _currentProblems = visibleProblems;
                _lastRefreshError = null;
                var lastUpdated = DateTime.Now;

                // zähle alle sichtbaren Probleme mit einer Severity >= unserem Error Schwellenwert
                var errorCount = visibleProblems.Count(p => p.Severity >= config.ErrorSeverityThreshold);

                // zähle alle sichtbaren Probleme die mindestens unserem Warnungs Schwellenwert entsprechen aber noch kein Error sind
                var warningCount = visibleProblems.Count(p =>
                    p.Severity >= config.WarningSeverityThreshold &&
                    p.Severity < config.ErrorSeverityThreshold
                );

                // Tray-Icon abhängig von Problemen setzen
                if (errorCount > 0)
                    SetTrayIcon("tray-error.ico");
                else if (warningCount > 0)
                    SetTrayIcon("tray-warning.ico");
                else
                    SetTrayIcon("tray-ok.ico");

                var fullText = BuildTrayToolTipText(
                    config,
                    visibleProblems,
                    errorCount,
                    warningCount,
                    lastUpdated
                );

                UpdateTrayToolTip(fullText);

                // Wenn das ProblemsWindow sichtbar ist Inhalt aktualisieren
                try
                {
                    if (_problemsWindow?.IsVisible == true)
                    {
                        _problemsWindow.UpdateProblems(_currentProblems);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                // Jeder Fehler beim Abrufen/Verarbeiten der Zabbix-Daten wird als Fehlerstatus angezeigt
                try
                {
                    SetTrayIcon("tray-error.ico");
                }
                catch { }

                _lastRefreshError = ex.Message;
                _currentProblems = new List<ZabbixProblem>();

                // Wenn ein API-/Netzwerkfehler passiert, keine alten Probleme mehr anzeigen
                try
                {
                    if (_problemsWindow?.IsVisible == true)
                    {
                        _problemsWindow.UpdateProblems(_currentProblems);
                    }
                }
                catch { }

                var lastUpdated = DateTime.Now;
                var cfg = _configService.Load();
                var lines = new List<string>
                {
                    string.IsNullOrWhiteSpace(cfg.AppName) ? "Zabbix Tray Monitor" : cfg.AppName,
                    "---------",
                    $"FEHLER: {ex.Message}",
                    "---------",
                    $"Zabbix-Server: {cfg.ZabbixUrl}",
                    $"Aktualisiert am: {lastUpdated:dd.MM.yyyy HH:mm:ss} Uhr"
                };

                UpdateTrayToolTip(string.Join(Environment.NewLine, lines));
            }
            finally // sicherstellen dass der Loading Indikator wieder weg ist auch wenn Fehler auftreten
            {
                _isRefreshing = false;

                try
                {
                    if (_problemsWindow?.IsVisible == true)
                    {
                        _problemsWindow.SetLoading(false);
                    }
                }
                catch { }
            }
        }

        private string BuildTrayToolTipText(
            ZabbixConfig config,
            List<ZabbixProblem> problems,
            int errorCount,
            int warningCount,
            DateTime lastUpdated)
        {
            // Tooltip bewusst kompakt halten:
            // nur Host + wichtigste Meldung anzeigen, keine Uhrzeit pro Item und keine Detailzeile
            var lines = new List<string>
    {
        string.IsNullOrWhiteSpace(config.AppName) ? "Zabbix Tray Monitor" : config.AppName,
        "---------"
    };

            if (errorCount == 0 && warningCount == 0)
            {
                lines.Add("Keine Probleme");
                lines.Add("---------");
                lines.Add($"Zabbix-Server: {config.ZabbixUrl}");
                lines.Add($"Aktualisiert am: {lastUpdated:dd.MM.yyyy HH:mm:ss} Uhr");
                return string.Join(Environment.NewLine, lines);
            }

            var errorProblems = problems
                .Where(p => p.Severity >= config.ErrorSeverityThreshold)
                .OrderByDescending(p => p.Severity)
                .ThenByDescending(p => p.Time)
                .ToList();

            var warningProblems = problems
                .Where(p =>
                    p.Severity >= config.WarningSeverityThreshold &&
                    p.Severity < config.ErrorSeverityThreshold)
                .OrderByDescending(p => p.Severity)
                .ThenByDescending(p => p.Time)
                .ToList();

            var errorItems = ProblemsMapper.BuildViewModels(errorProblems, config);
            var warningItems = ProblemsMapper.BuildViewModels(warningProblems, config);

            if (errorCount > 0 && warningCount > 0)
            {
                AddProblemSection(lines, "Fehler", errorCount, errorItems, 5);

                lines.Add("---------");

                AddProblemSection(lines, "Warnungen", warningCount, warningItems, 5);
            }
            else if (errorCount > 0)
            {
                AddProblemSection(lines, "Fehler", errorCount, errorItems, 10);
            }
            else if (warningCount > 0)
            {
                AddProblemSection(lines, "Warnungen", warningCount, warningItems, 10);
            }

            lines.Add("---------");
            lines.Add($"Zabbix-Server: {config.ZabbixUrl}");
            lines.Add($"Aktualisiert am: {lastUpdated:dd.MM.yyyy HH:mm:ss} Uhr");

            return string.Join(Environment.NewLine, lines);
        }

        private static void AddProblemSection(
            List<string> lines,
            string title,
            int count,
            List<ProblemListItem> problems,
            int maxVisibleItems)
        {
            lines.Add($"{title}: {count}");

            foreach (var problem in problems.Take(maxVisibleItems))
            {
                var host = string.IsNullOrWhiteSpace(problem.Host)
                    ? "Zabbix"
                    : problem.Host;

                var message = string.IsNullOrWhiteSpace(problem.Name)
                    ? "(kein Text)"
                    : problem.Name;

                host = CleanToolTipLine(host);
                message = CleanToolTipLine(message);

                lines.Add($"- {host}: {message}");
            }

            if (problems.Count > maxVisibleItems)
            {
                lines.Add("- [...]");
            }
        }

        private static string CleanToolTipLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private void SetTrayIcon(string iconFileName)
        {
            TrayIcon.IconSource = new BitmapImage(
                new Uri($"pack://application:,,,/Assets/{iconFileName}")
            );
        }

        private void TrayIcon_Exit_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();
            TrayIcon.Dispose();
            Application.Current.Shutdown();
        }

        private void TrayIcon_PreviewTrayToolTipOpen(object sender, RoutedEventArgs e)
        {
            // Während das Problemfenster sichtbar ist, keinen zusätzlichen Tray-Tooltip öffnen.
            if (_problemsWindow?.IsVisible == true)
            {
                e.Handled = true;
                return;
            }

            var config = _configService.Load();
            var delayMilliseconds = Math.Max(0, config.TrayToolTipDelayMilliseconds);

            // 0 ms bedeutet: Hardcodet darf den Tooltip wie gewohnt sofort öffnen.
            if (delayMilliseconds == 0)
                return;

            // Das automatische Öffnen zunächst unterdrücken. Sobald Windows meldet, dass
            // die Maus das Tray-Icon wieder verlassen hat, wird der Timer abgebrochen.
            e.Handled = true;
            _trayToolTipOpenRequested = true;

            _trayToolTipDelayTimer.Stop();
            _trayToolTipDelayTimer.Interval = TimeSpan.FromMilliseconds(delayMilliseconds);
            _trayToolTipDelayTimer.Start();
        }

        private void TrayIcon_PreviewTrayToolTipClose(object sender, RoutedEventArgs e)
        {
            // Native Close-Meldung nicht blockieren: Sie schließt auch einen von uns
            // nach Ablauf der Verzögerung geöffneten Custom-Tooltip zuverlässig.
            _trayToolTipOpenRequested = false;
            _trayToolTipDelayTimer.Stop();
        }

        private void TrayToolTipDelayTimer_Tick(object? sender, EventArgs e)
        {
            _trayToolTipDelayTimer.Stop();

            if (!_trayToolTipOpenRequested || _problemsWindow?.IsVisible == true)
                return;

            try
            {
                if (TrayIcon.ContextMenu?.IsOpen == true)
                    return;

                if (TrayIcon.TrayToolTipResolved is not null)
                {
                    TrayIcon.TrayToolTipResolved.IsOpen = true;
                }
            }
            catch { }
        }

        private void UpdateTrayToolTip(string fullText)
        {
            try
            {
                var tooltipText = string.IsNullOrWhiteSpace(fullText)
                    ? "Zabbix Tray Monitor"
                    : fullText;

                TrayToolTipTextBlock.Text = tooltipText;

                // Fallback für Systeme, auf denen kein Custom-Tooltip verfügbar ist.
                // Beim gesetzten TrayToolTip zeigt Hardcodet auf aktuellen Windows-Versionen
                // weiterhin den WPF-Custom-Tooltip an.
                var firstLine = tooltipText
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .FirstOrDefault();

                TrayIcon.ToolTipText = string.IsNullOrWhiteSpace(firstLine)
                    ? "Zabbix Tray Monitor"
                    : firstLine;
            }
            catch { }
        }

        private void HideTrayToolTip()
        {
            _trayToolTipOpenRequested = false;
            _trayToolTipDelayTimer.Stop();

            try
            {
                if (TrayIcon.TrayToolTipResolved is not null)
                {
                    TrayIcon.TrayToolTipResolved.IsOpen = false;
                }
            }
            catch { }
        }

        private void TrayIcon_Info_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();

            // Wenn Info- oder Einstellungsfenster bereits offen sind, bringe es nach vorne
            var existingInfoWin = Application.Current.Windows.OfType<Views.InfoWindow>().FirstOrDefault();
            if (existingInfoWin != null)
            {
                existingInfoWin.Activate();
                return;
            }

            var existingConfigWin = Application.Current.Windows.OfType<ZabbixConfigWindow>().FirstOrDefault();
            if (existingConfigWin != null)
            {
                existingConfigWin.Activate();
                return;
            }

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