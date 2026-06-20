using System.ComponentModel;
using System.Windows;
using ZabbixTrayMonitor.Models;

namespace ZabbixTrayMonitor.Views
{
    public partial class ProblemsWindow : Window
    {
        private bool _suppressLocationChanged = false;
        private readonly Func<Task<List<ZabbixProblem>>> _refreshAction;
        private readonly Action _openDashboardAction;
        private readonly int _warningSeverityThreshold;
        private readonly int _errorSeverityThreshold;

        public ProblemsWindow(
            List<ZabbixProblem> problems,
            int warningSeverityThreshold,
            int errorSeverityThreshold,
            Func<Task<List<ZabbixProblem>>> refreshAction,
            Action openDashboardAction)
        {
            InitializeComponent();

            _refreshAction = refreshAction;
            _openDashboardAction = openDashboardAction;
            _warningSeverityThreshold = warningSeverityThreshold;
            _errorSeverityThreshold = errorSeverityThreshold;

            // Theme wird global vom ThemeService angewendet
            LoadProblems(problems, warningSeverityThreshold, errorSeverityThreshold);

            // Wenn das Fenster den Fokus verliert soll das Fenster geschlossen werden ohne den Prozess zu killen
            Deactivated += (_, _) => Hide();

            // nicht verschieben -> wenn es verschoben wird, wird es instant resettet
            LocationChanged += ProblemsWindow_LocationChanged;
        }

        // Theme wird global vom ThemeService verwaltet

        public void UpdateProblems(List<ZabbixProblem> problems)
        {
            LoadProblems(problems, _warningSeverityThreshold, _errorSeverityThreshold);
            SetLoading(false);
        }

        public void SetLoading(bool loading)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingTextBlock.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void ProblemsWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (_suppressLocationChanged)
                return;

            // unten-rechts des Arbeitsbereichs mit kleinem Offset
            var work = SystemParameters.WorkArea;
            var desiredLeft = work.Right - Width - 2;
            var desiredTop = work.Bottom - Height - 2;

            if (Math.Abs(Left - desiredLeft) > 1 || Math.Abs(Top - desiredTop) > 1)
            {
                _suppressLocationChanged = true;
                Left = desiredLeft;
                Top = desiredTop;
                _suppressLocationChanged = false;
            }
        }

        private void LoadProblems(
            List<ZabbixProblem> problems,
            int warningSeverityThreshold,
            int errorSeverityThreshold)
        {
            var viewModels = problems
                .OrderByDescending(p => p.Severity)
                .ThenByDescending(p => p.Time)
                .Select(p => new ProblemListItem
                {
                    Status = GetStatus(p.Severity, warningSeverityThreshold, errorSeverityThreshold),
                    Severity = p.Severity,
                    Name = p.Name,
                    Time = p.Time.ToString("dd-MM-yyyy HH:mm:ss"),
                    Acknowledged = p.Acknowledged,
                    StatusColor = GetStatusColor(p.Severity, warningSeverityThreshold, errorSeverityThreshold),
                    Host = "Zabbix",
                })
                .ToList();

            ProblemsItemsControl.ItemsSource = viewModels;
            Title = $"Aktuelle Zabbix Probleme - {viewModels.Count}";
            LastUpdatedTextBlock.Text = $"Zuletzt aktualisiert: {DateTime.Now:HH:mm:ss}";

            // Wenn keine Probleme da sind, soll ein Text angezeigt werden, dass alles ok ist
            EmptyTextBlock.Visibility = viewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string GetStatusColor(int severity, int warningSeverityThreshold, int errorSeverityThreshold)
        {
            if (severity >= errorSeverityThreshold)
                return "#FF0015";

            if (severity >= warningSeverityThreshold)
                return "#F3C601";

            return "#808080";
        }

        private static string GetStatus(int severity, int warningSeverityThreshold, int errorSeverityThreshold)
        {
            if (severity >= errorSeverityThreshold)
                return "FEHLER";

            if (severity >= warningSeverityThreshold)
                return "WARNUNG";

            return "INFO";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            var problems = await _refreshAction();
            LoadProblems(problems, _warningSeverityThreshold, _errorSeverityThreshold);
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            _openDashboardAction();
            Hide();
        }

        private void ProblemItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                _openDashboardAction();
                Hide();
            }
            catch { }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private class ProblemListItem
        {
            public string Status { get; set; } = "";
            public int Severity { get; set; }
            public string Name { get; set; } = "";
            public string Time { get; set; } = "";
            public bool Acknowledged { get; set; }
            public string StatusColor { get; set; } = "#808080";
            public string Host { get; set; } = "Zabbix";
        }
    }
}