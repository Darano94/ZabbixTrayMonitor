namespace ZabbixTrayMonitor.Models
{
    public class ZabbixConfig
    {
        public string ZabbixUrl { get; set; } = "";
        public int PollIntervalSeconds { get; set; } = 60;
        public bool IgnoreCertificateErrors { get; set; } = false; // Für die Verbindung zum Zabbix-Server
    }
}
