using System.Collections.Generic;
using System.Linq;
using ZabbixTrayMonitor.Models;

// Mapped ZabbixProbleme in ProblemListItems fürs ProbllemWindow zum rendern
// dadurch keine Logik in ProblemsWindow.xaml.cs

namespace ZabbixTrayMonitor.Services
{
    public static class ProblemsMapper
    {
        public static List<ProblemListItem> BuildViewModels(List<ZabbixProblem>? problems, ZabbixConfig config)
        {
            if (problems == null) return new List<ProblemListItem>();

            return problems
                .OrderByDescending(p => p.Severity)
                .ThenByDescending(p => p.Time)
                .Select(p => new ProblemListItem
                {
                    Status = GetStatus(p.Severity, config.WarningSeverityThreshold, config.ErrorSeverityThreshold),
                    Severity = p.Severity,
                    Name = p.Name,
                    Time = p.Time.ToString("dd-MM-yyyy HH:mm:ss"),
                    Acknowledged = p.Acknowledged,
                    StatusColor = GetStatusColor(p.Severity, config),
                    Host = "Zabbix", // Todo: Platzhalter , für richtigen Endpunkt trigger.get callen
                })
                .ToList();
        }

        private static string GetStatusColor(int severity, ZabbixConfig config)
        {
            if (severity >= config.ErrorSeverityThreshold)
                return config.StatusColorError;

            if (severity >= config.WarningSeverityThreshold)
                return config.StatusColorWarning;

            return config.StatusColorInfo;
        }

        private static string GetStatus(int severity, int warningSeverityThreshold, int errorSeverityThreshold)
        {
            if (severity >= errorSeverityThreshold)
                return "FEHLER";

            if (severity >= warningSeverityThreshold)
                return "WARNUNG";

            return "INFO";
        }
    }
}
