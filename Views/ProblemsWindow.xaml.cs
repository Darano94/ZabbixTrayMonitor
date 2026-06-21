using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Services;

namespace ZabbixTrayMonitor.Views
{
    public partial class ProblemsWindow : Window
    {
        private bool _suppressLocationChanged = false; // Verhindert Endlosschleife im LocationChanged-Event
        private bool _isRefreshing = false; // verhindert mehrfaches paralleles Aktualisieren

        private readonly Func<Task<List<ZabbixProblem>>> _refreshAction;
        private readonly Action _openDashboardAction;
        private readonly ConfigService _configService; 

        public ProblemsWindow(
            List<ZabbixProblem> problems,
            Func<Task<List<ZabbixProblem>>> refreshAction,
            Action openDashboardAction,
            ConfigService configService)
        {
            InitializeComponent();

            // übergeben von externen Funktionen
            _refreshAction = refreshAction;
            _openDashboardAction = openDashboardAction;
            _configService = configService;

            ApplyProblems(problems);

            // Wenn das Fenster den Fokus verliert Fenster schließen werden ohne den Prozess zu killen
            Deactivated += (_, _) => Hide();

            // nicht verschieben -> wenn es verschoben wird instant resetten nach unten rechts
            Loaded += (_, _) => MoveToBottomRight();
            SizeChanged += (_, _) => MoveToBottomRight();
            LocationChanged += ProblemsWindow_LocationChanged;
        }

        public void UpdateProblems(List<ZabbixProblem> problems)
        {
            // Zugriff auf UI-Elemente muss im UI-Thread passieren
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateProblems(problems));
                return;
            }

            ApplyProblems(problems);
            SetLoading(false);
        }

        public void SetLoading(bool loading)
        {
            // Zugriff auf UI-Elemente muss im UI-Thread passieren
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetLoading(loading));
                return;
            }

            LoadingTextBlock.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyProblems(List<ZabbixProblem> problems)
        {
            var cfg = _configService.Load();
            var viewModels = ProblemsMapper.BuildViewModels(problems, cfg);

            ProblemsItemsControl.ItemsSource = viewModels;

            Title = $"{cfg.AppName} - Aktuelle Zabbix Probleme - {viewModels.Count}";
            LastUpdatedTextBlock.Text = $"Zuletzt aktualisiert: {DateTime.Now:HH:mm:ss}";

            EmptyTextBlock.Visibility = viewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ProblemsScrollViewer.Visibility = viewModels.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ProblemsWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (_suppressLocationChanged) // überspringen wenn Position gerade gesetzt wird damit keine Endlosschleife
                return;

            MoveToBottomRight();
        }

        private void MoveToBottomRight()
        {
            if (_suppressLocationChanged)
                return;

            // unten-rechts des Arbeitsbereichs mit kleinem Offset
            var work = SystemParameters.WorkArea;

            var windowWidth = ActualWidth > 0 ? ActualWidth : Width;
            var windowHeight = ActualHeight > 0 ? ActualHeight : Height;

            var desiredLeft = work.Right - windowWidth - 2;
            var desiredTop = work.Bottom - windowHeight - 2;

            if (Math.Abs(Left - desiredLeft) <= 1 && Math.Abs(Top - desiredTop) <= 1)
                return;

            _suppressLocationChanged = true;
            Left = desiredLeft;
            Top = desiredTop;
            _suppressLocationChanged = false;
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing)
                return;

            try
            {
                _isRefreshing = true;
                SetLoading(true);

                var problems = await _refreshAction();
                UpdateProblems(problems);
            }
            catch (Exception ex)
            {
                SetLoading(false);

                MessageBox.Show(
                    this,
                    $"Aktualisierung fehlgeschlagen:" +
                    $"\n\n{ex.Message}",
                    "Zabbix Probleme aktualisieren",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                _isRefreshing = false;
            }
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
    }
}