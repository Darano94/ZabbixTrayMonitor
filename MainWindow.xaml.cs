using System.Windows;
using System.Windows.Media.Imaging;
using ZabbixTrayMonitor.Services;

namespace ZabbixTrayMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ConfigService _configService = new();

        private int _testState = 0;
        public MainWindow()
        {
            InitializeComponent();

            Hide(); // Mainframe ausblenden

            if (!_configService.ConfigExists())
            {
                var configWindow = new ZabbixConfigWindow();
                configWindow.ShowDialog();
            }
        }
        private void TrayIcon_Settings_Click(object sender, RoutedEventArgs e)
        {
            var configWindow = new ZabbixConfigWindow();
            configWindow.ShowDialog();
        }

        private void TrayIcon_Refresh_Click(object sender, RoutedEventArgs e)
        {
            _testState++;

            if (_testState > 2)
                _testState = 0;

            switch (_testState)
            {
                case 0:
                    TrayIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/tray-ok.ico"));
                    TrayIcon.ToolTipText = "Zabbix: Keine Probleme";
                    break;

                case 1:
                    TrayIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/tray-warning.ico"));
                    TrayIcon.ToolTipText = "Zabbix: 3 Warnungen";
                    break;

                case 2:
                    TrayIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/tray-error.ico"));
                    TrayIcon.ToolTipText = "Zabbix: 2 kritische Probleme";
                    break;
            }
        }

        private void TrayIcon_Exit_Click(object sender, RoutedEventArgs e)
        {
            TrayIcon.Dispose();
            Application.Current.Shutdown();
        }
    }
}