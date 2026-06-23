using System;

// Repräsentiert eine Zabbix-Problem-Entity aus der API JSON-RPC Response

namespace ZabbixTrayMonitor.Models
{
    public class ZabbixProblem
    {
        public string EventId { get; set; } = "";
        public string TriggerId { get; set; } = "";
        public string HostName { get; set; } = "";
        public string MonitoredObject { get; set; } = "";
        public string Name { get; set; } = "";
        public string OperationalData { get; set; } = "";
        public int Severity { get; set; }
        public DateTime Time { get; set; }
        public bool Acknowledged { get; set; } // todo
    }
}
