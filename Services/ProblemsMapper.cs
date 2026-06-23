using System;
using System.Collections.Generic;
using System.Linq;
using ZabbixTrayMonitor.Models;

// Mapped ZabbixProbleme in ProblemListItems fürs ProblemsWindow zum Rendern
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
                .Select(p =>
                {
                    var hostName = string.IsNullOrWhiteSpace(p.HostName) ? "Zabbix" : p.HostName;
                    var displayText = BuildDisplayText(p, hostName);

                    return new ProblemListItem
                    {
                        Status = GetStatus(p.Severity, config.WarningSeverityThreshold, config.ErrorSeverityThreshold),
                        StatusInitial = GetStatusInitial(p.Severity, config.WarningSeverityThreshold, config.ErrorSeverityThreshold),
                        Severity = p.Severity,

                        // wichtigste Meldung fett anzeigen
                        Name = displayText.title,

                        // technischer Kontext / was überwacht wird darunter anzeigen
                        Message = displayText.message,

                        Time = p.Time.ToString("dd-MM-yyyy HH:mm:ss"),
                        Acknowledged = p.Acknowledged,
                        StatusColor = GetStatusColor(p.Severity, config),
                        Host = hostName
                    };
                })
                .ToList();
        }

        // Baut die Anzeige fürs Problemfenster
        // Zeile 1 im XAML: Host + Datum
        // Zeile 2: wichtigste Meldung / Problemzustand
        // Zeile 3: technischer Kontext / was überwacht wird
        private static (string title, string message) BuildDisplayText(ZabbixProblem problem, string hostName)
        {
            var problemName = RemoveHostPrefix(CleanText(problem.Name), hostName);
            var monitoredObject = CleanText(problem.MonitoredObject);
            var operationalData = CleanText(problem.OperationalData);

            if (string.IsNullOrWhiteSpace(problemName) && string.IsNullOrWhiteSpace(monitoredObject))
                return ("(kein Text)", operationalData);

            // Beispiel:
            // ProblemName: "Paperless Web nicht erreichbar"
            // MonitoredObject: "Failed step of scenario \"Paperless HTTPS\"."
            // Anzeige: "nicht erreichbar" fett, Kontext darunter
            var stateFromName = ExtractStateFromProblemName(problemName, hostName);

            if (!string.IsNullOrWhiteSpace(stateFromName))
            {
                var context = BuildContext(monitoredObject, operationalData, problemName);
                return (stateFromName, context);
            }

            // Beispiel:
            // ProblemName: "Linux: Zabbix agent is not available"
            // OperationalData: "not available (0)" oder ähnlich
            // Anzeige: voller Problemname fett, Item/OperationalData darunter
            if (!string.IsNullOrWhiteSpace(monitoredObject) && !IsSameText(monitoredObject, problemName))
            {
                var detail = BuildContext(monitoredObject, operationalData, problemName);
                return (problemName, detail);
            }

            if (!string.IsNullOrWhiteSpace(operationalData))
            {
                return (problemName, operationalData);
            }

            return (problemName, "");
        }

        // Versucht aus Problemnamen den eigentlichen Zustand zu ziehen
        // zB "Paperless Web nicht erreichbar" -> "nicht erreichbar"
        private static string ExtractStateFromProblemName(string problemName, string hostName)
        {
            var name = CleanText(problemName);
            var host = CleanText(hostName);

            if (string.IsNullOrWhiteSpace(name))
                return "";

            if (!string.IsNullOrWhiteSpace(host) &&
                name.StartsWith(host, StringComparison.OrdinalIgnoreCase))
            {
                var rest = name.Substring(host.Length).Trim();
                rest = rest.TrimStart(':', '-', '–', '—').Trim();

                if (!string.IsNullOrWhiteSpace(rest))
                    return rest;
            }

            var notIndex = name.IndexOf(" nicht ", StringComparison.OrdinalIgnoreCase);
            if (notIndex > 0 && notIndex < name.Length - 1)
            {
                return name.Substring(notIndex + 1).Trim();
            }

            return "";
        }

        // Baut die Detailzeile aus Item/Trigger-Info und OperationalData
        // vermeidet einfache Dopplungen
        private static string BuildContext(string monitoredObject, string operationalData, string problemName)
        {
            var monitored = CleanText(monitoredObject);
            var opData = CleanText(operationalData);
            var name = CleanText(problemName);

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(monitored) && !ContainsNormalized(name, monitored))
                parts.Add(monitored);

            if (!string.IsNullOrWhiteSpace(opData) &&
                !ContainsNormalized(name, opData) &&
                !parts.Any(p => IsSameText(p, opData)))
            {
                parts.Add(opData);
            }

            return string.Join(" - ", parts);
        }

        private static string RemoveHostPrefix(string value, string hostName)
        {
            var text = CleanText(value);
            var host = CleanText(hostName);

            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(host))
                return text;

            if (!text.StartsWith(host, StringComparison.OrdinalIgnoreCase))
                return text;

            var rest = text.Substring(host.Length).Trim();
            rest = rest.TrimStart(':', '-', '–', '—').Trim();

            return string.IsNullOrWhiteSpace(rest) ? text : rest;
        }

        private static bool IsSameText(string left, string right)
        {
            return string.Equals(CleanText(left), CleanText(right), StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsNormalized(string text, string part)
        {
            var cleanText = CleanText(text);
            var cleanPart = CleanText(part);

            if (string.IsNullOrWhiteSpace(cleanText) || string.IsNullOrWhiteSpace(cleanPart))
                return false;

            return cleanText.IndexOf(cleanPart, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CleanText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            return value.Replace("\r", " ").Replace("\n", " ").Trim();
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
                return "ERROR";

            if (severity >= warningSeverityThreshold)
                return "WARNING";

            return "INFO";
        }

        private static string GetStatusInitial(int severity, int warningSeverityThreshold, int errorSeverityThreshold)
        {
            if (severity >= errorSeverityThreshold)
                return "E";

            if (severity >= warningSeverityThreshold)
                return "W";

            return "I";
        }
    }
}