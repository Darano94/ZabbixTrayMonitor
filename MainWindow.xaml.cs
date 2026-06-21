using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        private List<ZabbixProblem> _currentProblems = new();
        private ProblemsWindow? _problemsWindow;
        private bool _isRefreshing = false;
        private bool _ignoreNextTrayLeftClick = false;
        // Unterdrücke das Öffnen des eigenen Tooltips kurz nachdem das Kontextmenü geöffnet wurde
        private DateTime _suppressTrayToolTipUntil = DateTime.MinValue;

        private string? _lastToolTipFullText;
        private Window? _trayToolTipWindow;
        private Border? _trayToolTipBorder;
        private TextBlock? _trayToolTipTextBlock;
        private bool _trayToolTipVisible = false;
        private NativePoint? _lastTrayMousePosition;
        private readonly DispatcherTimer _trayToolTipOpenTimer = new();
        private readonly DispatcherTimer _trayToolTipCloseTimer = new();

        private const int TrayMouseLeaveTolerancePixels = 48;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint lpPoint);

        public MainWindow()
        {
            InitializeComponent();

            Hide(); // Mainframe ausblenden

            // Native Tooltips deaktivieren, wir nutzen eigenes Tooltip-Fenster
            try
            {
                ClearNativeTrayToolTip();
                _lastToolTipFullText = "Zabbix Tray Monitor\n---------\nInitialisiere...";
                EnsureTrayToolTipInstance(_lastToolTipFullText);
            }
            catch { }

            _trayToolTipOpenTimer.Interval = TimeSpan.FromMilliseconds(350);
            _trayToolTipOpenTimer.Tick += TrayToolTipOpenTimer_Tick;

            _trayToolTipCloseTimer.Interval = TimeSpan.FromMilliseconds(100);
            _trayToolTipCloseTimer.Tick += TrayToolTipCloseTimer_Tick;
            _trayToolTipCloseTimer.Start();

            Application.Current.Deactivated += (_, _) => HideTrayToolTip();

            // RefreshTimer für Probleme initialisieren abhängig vom Pollintervall in den Einstellungen
            _refreshTimer.Tick += async (_, _) => await RefreshProblemsAsync();

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

            // Tooltip kurz unterdrücken, damit er beim Kontextmenü nicht dazwischenfunkt
            _suppressTrayToolTipUntil = DateTime.Now.AddSeconds(1);

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
                // Native Tooltips deaktivieren, wir setzen unten nur noch unser eigenes Tooltip-Fenster
                ClearNativeTrayToolTip();

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

                // Tray-Icon abhängig von Problemen setzen
                if (errorCount > 0)
                    SetTrayIcon("tray-error.ico");
                else if (warningCount > 0)
                    SetTrayIcon("tray-warning.ico");
                else
                    SetTrayIcon("tray-ok.ico");

                var fullText = BuildTrayToolTipText(
                    config,
                    problems,
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

                var lastUpdated = DateTime.Now;
                var cfg = _configService.Load();

                var lines = new List<string>
                {
                    string.IsNullOrWhiteSpace(cfg.AppName) ? "Zabbix Tray Monitor" : cfg.AppName,
                    "---------",
                    $"FEHLER: {ex.Message}",
                    "---------",
                    $"Aktualisiert um: {lastUpdated:HH:mm:ss} Uhr",
                    $"Zabbix-Server: {cfg.ZabbixUrl}"
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
            // Aufbau Tooltip: AppName, separator, Fehler-Sektion, separator, Warnungen-Sektion, separator, Aktualisiert + Zabbix-Server (unten)
            var lines = new List<string>
            {
                string.IsNullOrWhiteSpace(config.AppName) ? "Zabbix Tray Monitor" : config.AppName,
                "---------"
            };

            // Wenn es weder Fehler noch Warnungen gibt, klaren Hinweis anzeigen und frühzeitig beenden
            if (errorCount == 0 && warningCount == 0)
            {
                lines.Add("Keine Probleme");
                lines.Add("---------");
                lines.Add($"Aktualisiert um: {lastUpdated:HH:mm:ss} Uhr");
                lines.Add($"Zabbix-Server: {config.ZabbixUrl}");
                return string.Join(Environment.NewLine, lines);
            }

            var errorItemsAll = problems
                .Where(p => p.Severity >= config.ErrorSeverityThreshold)
                .OrderByDescending(p => p.Severity)
                .ThenByDescending(p => p.Time)
                .ToList();

            var warningItemsAll = problems
                .Where(p =>
                    p.Severity >= config.WarningSeverityThreshold &&
                    p.Severity < config.ErrorSeverityThreshold)
                .OrderByDescending(p => p.Severity)
                .ThenByDescending(p => p.Time)
                .ToList();

            if (errorCount > 0 && warningCount > 0)
            {
                // Fehler + Warnungen: maximal 5 Fehler und maximal 5 Warnungen anzeigen
                AddProblemSection(lines, "Fehler", errorCount, errorItemsAll, 5);

                lines.Add("---------");

                AddProblemSection(lines, "Warnungen", warningCount, warningItemsAll, 5);
            }
            else if (errorCount > 0)
            {
                // Nur Fehler: maximal 10 Fehler anzeigen, keine Warnungen = 0 anzeigen
                AddProblemSection(lines, "Fehler", errorCount, errorItemsAll, 10);
            }
            else if (warningCount > 0)
            {
                // Nur Warnungen: maximal 10 Warnungen anzeigen, keine Fehler = 0 anzeigen
                AddProblemSection(lines, "Warnungen", warningCount, warningItemsAll, 10);
            }

            // Abschluss: Aktualisiert + Zabbix-Server ganz unten
            lines.Add("---------");
            lines.Add($"Aktualisiert um: {lastUpdated:HH:mm:ss} Uhr");
            lines.Add($"Zabbix-Server: {config.ZabbixUrl}");

            return string.Join(Environment.NewLine, lines);
        }

        private static void AddProblemSection(
            List<string> lines,
            string title,
            int count,
            List<ZabbixProblem> problems,
            int maxVisibleItems)
        {
            lines.Add($"{title}: {count}");

            foreach (var p in problems.Take(maxVisibleItems))
            {
                var text = string.IsNullOrWhiteSpace(p.Name) ? "(kein Text)" : p.Name;

                // Entferne Zeilenumbrüche im Text, damit Tooltip-Layout stabil bleibt
                text = text.Replace("\r", " ").Replace("\n", " ").Trim();

                lines.Add($"- {text}");
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
            HideTrayToolTip();
            TrayIcon.Dispose();
            Application.Current.Shutdown();
        }

        private void TrayIcon_TrayMouseMove(object sender, RoutedEventArgs e)
        {
            if (GetCursorPos(out var point))
            {
                _lastTrayMousePosition = point;
            }

            // Wenn das ProblemsWindow geöffnet ist, kein Tooltip starten
            if (_problemsWindow?.IsVisible == true)
                return;

            if (_trayToolTipVisible)
                return;

            if (_trayToolTipOpenTimer.IsEnabled)
                return;

            _trayToolTipOpenTimer.Start();
        }

        private void TrayToolTipOpenTimer_Tick(object? sender, EventArgs e)
        {
            _trayToolTipOpenTimer.Stop();

            // Wenn das ProblemsWindow geöffnet ist, Tooltip nicht zeigen
            if (_problemsWindow?.IsVisible == true)
                return;

            // Wenn kurz zuvor das Kontextmenü geöffnet wurde, Tooltip nicht zeigen
            if (DateTime.Now < _suppressTrayToolTipUntil)
                return;

            if (string.IsNullOrWhiteSpace(_lastToolTipFullText))
                return;

            if (_lastTrayMousePosition == null)
                return;

            if (!GetCursorPos(out var currentPosition))
                return;

            var lastPosition = _lastTrayMousePosition.Value;

            var distanceX = Math.Abs(currentPosition.X - lastPosition.X);
            var distanceY = Math.Abs(currentPosition.Y - lastPosition.Y);

            // Wenn die Maus während der Verzögerung schon weg ist, keinen Tooltip öffnen
            if (distanceX > TrayMouseLeaveTolerancePixels || distanceY > TrayMouseLeaveTolerancePixels)
                return;

            ShowTrayToolTip(currentPosition);
        }

        private void TrayToolTipCloseTimer_Tick(object? sender, EventArgs e)
        {
            if (!_trayToolTipVisible && !_trayToolTipOpenTimer.IsEnabled)
                return;

            if (_lastTrayMousePosition == null)
                return;

            if (!GetCursorPos(out var currentPosition))
                return;

            var lastPosition = _lastTrayMousePosition.Value;

            var distanceX = Math.Abs(currentPosition.X - lastPosition.X);
            var distanceY = Math.Abs(currentPosition.Y - lastPosition.Y);

            // Sobald die Maus deutlich vom TrayIcon weg ist, Tooltip schließen
            if (distanceX > TrayMouseLeaveTolerancePixels || distanceY > TrayMouseLeaveTolerancePixels)
            {
                HideTrayToolTip();
            }
        }

        private void UpdateTrayToolTip(string fullText)
        {
            _lastToolTipFullText = fullText;

            try
            {
                ClearNativeTrayToolTip();
                EnsureTrayToolTipInstance(fullText);

                if (_trayToolTipTextBlock != null)
                {
                    _trayToolTipTextBlock.Text = fullText;
                }
            }
            catch { }
        }

        private void ShowTrayToolTip(NativePoint cursorPosition)
        {
            if (string.IsNullOrWhiteSpace(_lastToolTipFullText))
                return;

            // Wenn Kontextmenü offen ist oder wir gerade die Anzeige unterdrücken, nichts tun
            try
            {
                if (TrayIcon?.ContextMenu?.IsOpen == true)
                    return;
            }
            catch { }

            if (DateTime.Now < _suppressTrayToolTipUntil)
                return;

            // Wenn das ProblemsWindow geöffnet ist, kein Tooltip anzeigen
            if (_problemsWindow?.IsVisible == true)
                return;

            try
            {
                ClearNativeTrayToolTip();
                EnsureTrayToolTipInstance(_lastToolTipFullText);

                if (_trayToolTipWindow == null)
                    return;

                if (_trayToolTipTextBlock != null)
                {
                    _trayToolTipTextBlock.Text = _lastToolTipFullText;
                }

                _trayToolTipWindow.Show();
                _trayToolTipWindow.UpdateLayout();

                var position = ConvertScreenPixelToWpfPoint(cursorPosition);

                var tooltipWidth = _trayToolTipWindow.ActualWidth;
                var tooltipHeight = _trayToolTipWindow.ActualHeight;

                var left = position.X - tooltipWidth + 24;
                var top = position.Y - tooltipHeight - 18;

                var workArea = SystemParameters.WorkArea;

                if (left < workArea.Left)
                    left = workArea.Left + 4;

                if (top < workArea.Top)
                    top = workArea.Top + 4;

                if (left + tooltipWidth > workArea.Right)
                    left = workArea.Right - tooltipWidth - 4;

                if (top + tooltipHeight > workArea.Bottom)
                    top = workArea.Bottom - tooltipHeight - 4;

                _trayToolTipWindow.Left = left;
                _trayToolTipWindow.Top = top;

                _trayToolTipVisible = true;
            }
            catch
            {
                _trayToolTipVisible = false;
            }
        }

        private void HideTrayToolTip()
        {
            _trayToolTipOpenTimer.Stop();

            try
            {
                _trayToolTipWindow?.Hide();
            }
            catch { }

            _trayToolTipVisible = false;
        }

        private void ClearNativeTrayToolTip()
        {
            try
            {
                TrayIcon.ToolTipText = null;
                TrayIcon.ToolTip = null;
                TrayIcon.TrayToolTip = null;
            }
            catch { }
        }

        private void EnsureTrayToolTipInstance(string text)
        {
            if (_trayToolTipWindow != null && _trayToolTipBorder != null && _trayToolTipTextBlock != null)
            {
                _trayToolTipTextBlock.Text = text;
                return;
            }

            _trayToolTipTextBlock = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520
            };

            _trayToolTipBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(235, 32, 32, 32)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8, 10, 8),
                Child = _trayToolTipTextBlock
            };

            _trayToolTipWindow = new Window
            {
                Content = _trayToolTipBorder,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                Focusable = false,
                IsHitTestVisible = false
            };
        }

        private Point ConvertScreenPixelToWpfPoint(NativePoint point)
        {
            try
            {
                var source = PresentationSource.FromVisual(this);
                var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

                return transform.Transform(new Point(point.X, point.Y));
            }
            catch
            {
                return new Point(point.X, point.Y);
            }
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