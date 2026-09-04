using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Services;

namespace ZabbixTrayMonitor.Views
{
    public partial class ProblemsWindow : Window
    {
        private bool _suppressLocationChanged = false; // Verhindert die Endlosschleife beim Setzen der Position im LocationChanged-Event
        private bool _isRefreshing = false; // verhindert mehrfaches paralleles Aktualisieren
        private bool _isProblemActionRunning = false; // verhindert doppelte Bestätigen-/Unterdrücken-Aktionen
        private bool _keepOpenOnDeactivate = false; // z. B. während "Unterdrücken bis..."-Dialog geöffnet ist

        private readonly Func<Task<List<ZabbixProblem>>> _refreshAction; // Funktion, die von extern übergeben wird damit Logik nicht in der View liegt
        private readonly Action _openDashboardAction; // Funktion, die von extern übergeben wird damit Logik nicht in der View liegt
        private readonly Func<string, DateTime?, string, Task> _problemAction; // API-Aktion wird von MainWindow ausgeführt
        private readonly ConfigService _configService;

        public ProblemsWindow(
            List<ZabbixProblem> problems,
            Func<Task<List<ZabbixProblem>>> refreshAction,
            Action openDashboardAction,
            Func<string, DateTime?, string, Task> problemAction,
            ConfigService configService)
        {
            InitializeComponent();

            // übergeben von externen Funktionen
            _refreshAction = refreshAction;
            _openDashboardAction = openDashboardAction;
            _problemAction = problemAction;

            // Konfigurationsservice speichern und ViewModels erstellen
            _configService = configService;

            ApplyProblems(problems);

            // Wenn das Fenster den Fokus verliert soll das Fenster geschlossen werden ohne den Prozess zu killen
            Deactivated += ProblemsWindow_Deactivated;

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

        private void ProblemsWindow_Deactivated(object? sender, EventArgs e)
        {
            if (_keepOpenOnDeactivate)
                return;

            Hide();
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

        // Mapping und Statuslogik wurde in ProblemsMapper ausgelagert
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
                MessageBox.Show(
                    this,
                    $"Aktualisierung fehlgeschlagen:{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                    "Zabbix Probleme aktualisieren",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                _isRefreshing = false;
                SetLoading(false);
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

        private void ProblemContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
                return;

            if (contextMenu.PlacementTarget is FrameworkElement placementTarget)
            {
                // ContextMenu liegt außerhalb des normalen WPF Visual Trees.
                // DataContext deshalb explizit von der Problemzeile übernehmen.
                contextMenu.DataContext = placementTarget.DataContext;
            }

            if (contextMenu.Items.Count > 0 &&
                contextMenu.Items[0] is MenuItem rootMenuItem)
            {
                var problem = contextMenu.DataContext as ProblemListItem;
                rootMenuItem.IsEnabled =
                    !_isProblemActionRunning &&
                    problem != null &&
                    !string.IsNullOrWhiteSpace(problem.EventId);

                // Bei jedem Öffnen mit der aktuell konfigurierten Standardnachricht starten.
                // Änderungen gelten nur für diese eine Aktion und werden nicht in die Config geschrieben.
                var messageTextBox = GetProblemActionMessageTextBox(rootMenuItem);
                if (messageTextBox != null)
                {
                    var config = _configService.Load();
                    messageTextBox.Text = GetDefaultAcknowledgeMessage(config);
                    messageTextBox.IsEnabled = rootMenuItem.IsEnabled;
                }
            }
        }

        private async void AcknowledgeProblem_Click(object sender, RoutedEventArgs e)
        {
            var problem = GetProblemFromMenuItem(sender);
            if (problem == null)
                return;

            await ExecuteProblemActionAsync(problem, null, GetProblemActionMessage(sender));
        }

        private async void Suppress5Minutes_Click(object sender, RoutedEventArgs e)
        {
            var problem = GetProblemFromMenuItem(sender);
            if (problem == null)
                return;

            await ExecuteProblemActionAsync(problem, DateTime.Now.AddMinutes(5), GetProblemActionMessage(sender));
        }

        private async void Suppress15Minutes_Click(object sender, RoutedEventArgs e)
        {
            var problem = GetProblemFromMenuItem(sender);
            if (problem == null)
                return;

            await ExecuteProblemActionAsync(problem, DateTime.Now.AddMinutes(15), GetProblemActionMessage(sender));
        }

        private async void Suppress1Hour_Click(object sender, RoutedEventArgs e)
        {
            var problem = GetProblemFromMenuItem(sender);
            if (problem == null)
                return;

            await ExecuteProblemActionAsync(problem, DateTime.Now.AddHours(1), GetProblemActionMessage(sender));
        }

        private async void Suppress3Hours_Click(object sender, RoutedEventArgs e)
        {
            var problem = GetProblemFromMenuItem(sender);
            if (problem == null)
                return;

            await ExecuteProblemActionAsync(problem, DateTime.Now.AddHours(3), GetProblemActionMessage(sender));
        }

        private async void Suppress1Day_Click(object sender, RoutedEventArgs e)
        {
            var problem = GetProblemFromMenuItem(sender);
            if (problem == null)
                return;

            await ExecuteProblemActionAsync(problem, DateTime.Now.AddDays(1), GetProblemActionMessage(sender));
        }

        private async void SuppressUntil_Click(object sender, RoutedEventArgs e)
        {
            var problem = GetProblemFromMenuItem(sender);
            if (problem == null)
                return;

            var dialog = new SuppressUntilWindow
            {
                Owner = this
            };

            bool? result;

            try
            {
                // Der ProblemsWindow-Deactivated-Handler darf das Fenster nicht verstecken,
                // während der modale Unterdrücken-bis-Dialog geöffnet ist.
                _keepOpenOnDeactivate = true;
                result = dialog.ShowDialog();
            }
            finally
            {
                _keepOpenOnDeactivate = false;
            }

            if (result != true || !dialog.SelectedDateTime.HasValue)
                return;

            await ExecuteProblemActionAsync(problem, dialog.SelectedDateTime.Value, GetProblemActionMessage(sender));
        }

        private ProblemListItem? GetProblemFromMenuItem(object sender)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is ProblemListItem problem)
            {
                return problem;
            }

            return null;
        }

        private string GetProblemActionMessage(object sender)
        {
            var config = _configService.Load();
            var fallbackMessage = GetDefaultAcknowledgeMessage(config);

            if (sender is not MenuItem menuItem)
                return fallbackMessage;

            // Die eigentlichen Aktionspunkte liegen direkt im Untermenü von "Alarm bearbeiten".
            // Über den enthaltenden ItemsControl kommen wir an den dort eingebetteten Nachrichten-Editor.
            var rootMenuItem = ItemsControl.ItemsControlFromItemContainer(menuItem) as MenuItem
                ?? menuItem.Parent as MenuItem;

            var messageTextBox = rootMenuItem == null
                ? null
                : GetProblemActionMessageTextBox(rootMenuItem);

            return string.IsNullOrWhiteSpace(messageTextBox?.Text)
                ? fallbackMessage
                : messageTextBox.Text.Trim();
        }

        private static TextBox? GetProblemActionMessageTextBox(MenuItem rootMenuItem)
        {
            foreach (var item in rootMenuItem.Items)
            {
                if (item is not MenuItem menuItem ||
                    !string.Equals(menuItem.Tag as string, "ProblemActionMessageEditor", StringComparison.Ordinal))
                {
                    continue;
                }

                if (menuItem.Header is StackPanel panel)
                {
                    foreach (UIElement child in panel.Children)
                    {
                        if (child is TextBox textBox)
                            return textBox;
                    }
                }
            }

            return null;
        }

        private static string GetDefaultAcknowledgeMessage(ZabbixConfig config)
        {
            return ZabbixConfig.ResolveAcknowledgeMessage(config.AcknowledgeMessage);
        }

        private void ProblemActionMessageTextBox_PreviewMouseLeftButtonDown(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.IsKeyboardFocusWithin)
                return;

            // Ein Klick reicht zum Überschreiben der Standardnachricht: Fokus setzen und alles markieren.
            e.Handled = true;
            textBox.Focus();
            textBox.SelectAll();
        }

        private async Task ExecuteProblemActionAsync(ProblemListItem problem, DateTime? suppressUntil, string message)
        {
            if (_isProblemActionRunning)
                return;

            try
            {
                _isProblemActionRunning = true;
                SetLoading(true);

                // API-Logik bleibt außerhalb des Views und wird über den Callback aus MainWindow ausgeführt.
                await _problemAction(problem.EventId, suppressUntil, message);

                var config = _configService.Load();

                if (config.RefreshAfterProblemAction)
                {
                    var problems = await _refreshAction();
                    UpdateProblems(problems);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"Alarm konnte nicht bearbeitet werden:{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                    "Alarm bearbeiten",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                _isProblemActionRunning = false;
                SetLoading(false);
            }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
