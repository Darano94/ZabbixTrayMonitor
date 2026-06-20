namespace ZabbixTrayMonitor.Models
{
    public class ZabbixConfig
    {
        public string ZabbixUrl { get; set; } = "";
        public string ZabbixDashboardUrl { get; set; } = "";
        public int PollIntervalSeconds { get; set; } = 60;
        public bool IgnoreCertificateErrors { get; set; } = false; // Für die Verbindung zum Zabbix-Server
        public int WarningSeverityThreshold { get; set; } = 2; // mapped von der Zabbix-Api returnten Severity-Wert auf unseren Severityschwellenwert
        public int ErrorSeverityThreshold { get; set; } = 4; // mapped von der Zabbix-Api returnten Severity-Wert auf unseren Severityschwellenwert
        public bool UseDarkMode { get; set; } = true; 
    }
}
