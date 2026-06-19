// Repräsentiert eine Zabbix-Problem-Entity aus der JSONRPC response

namespace ZabbixTrayMonitor.Models
{
    public class ZabbixProblem
    {
        public string EventId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Severity { get; set; }
        public DateTime Time { get; set; }
        public bool Acknowledged { get; set; }
    }
}