using System;

// ViewModel-Eintrag für ProblemsWindow

namespace ZabbixTrayMonitor.Models
{
    public class ProblemListItem
    {
        public string EventId { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusInitial { get; set; } = "";
        public int Severity { get; set; }
        public string Name { get; set; } = "";
        public string Message { get; set; } = "";
        public string Time { get; set; } = "";
        public bool Acknowledged { get; set; }
        public bool Suppressed { get; set; }
        public DateTime? SuppressedUntil { get; set; }
        public string StatusColor { get; set; } = "#808080";
        public string Host { get; set; } = "Zabbix";
    }
}
