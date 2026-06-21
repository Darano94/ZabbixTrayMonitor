// Repräsentiert die gespeicherte Zabbix-Konfiguration

namespace ZabbixTrayMonitor.Models
{
    public class ZabbixConfig
    {
        public string ZabbixUrl { get; set; } = "";
        public string ZabbixApiEndpoint { get; set; } = "/api_jsonrpc.php";
        public string ZabbixDashboardUrl { get; set; } = "";

        public int PollIntervalSeconds { get; set; } = 60;
        public bool IgnoreCertificateErrors { get; set; } = false; // Ignoriert SSL Fehler zB. bei self-signed Zertifikaten
        public bool UseDarkMode { get; set; } = true;

        public string AppName { get; set; } = "ZabbixTrayMonitor";

        // Schwellenwerte für die von der Zabbix API zurückgegebenen Severity-Werte
        /**
            0 = Not classified
            1 = Information
            2 = Warning
            3 = Average
            4 = High
            5 = Disaster
         */
        public int WarningSeverityThreshold { get; set; } = 2;
        public int ErrorSeverityThreshold { get; set; } = 4;

        // Default Farben für Statusanzeige und ProblemsWindow
        public string StatusColorError { get; set; } = "#FF0015";
        public string StatusColorWarning { get; set; } = "#F3C601";
        public string StatusColorInfo { get; set; } = "#808080";

        // Default Credential Einstellungen
        public string CredentialTargetSuffix { get; set; } = "ApiToken"; // Suffix nach {AppName}
        public string CredentialUsername { get; set; } = "api-token";
    }
}