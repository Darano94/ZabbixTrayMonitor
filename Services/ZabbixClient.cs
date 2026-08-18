using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ZabbixTrayMonitor.Models;

// Kommunikation mit der Zabbix API
// Zabbix API nutzt JSONRPC

namespace ZabbixTrayMonitor.Services
{
    public class ZabbixClient
    {
        private const string DefaultZabbixApiEndpoint = "/api_jsonrpc.php";

        // Wiederverwendete HttpClient-Instanzen: eine standard, eine mit zertbypass
        private static readonly HttpClient _defaultClient = new HttpClient();
        private static readonly HttpClient _insecureClient;

        static ZabbixClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator; // Alle Zertifikatsfehler ignorieren zB bei self-signed
            _insecureClient = new HttpClient(handler);
        }

        private static HttpClient GetClient(bool ignoreCertificateErrors)
        {
            return ignoreCertificateErrors ? _insecureClient : _defaultClient;
        }

        public async Task<List<ZabbixProblem>> GetProblemsAsync(
            string zabbixUrl,
            string zabbixApiEndpoint,
            string apiToken,
            int minimumSeverity,
            bool ignoreCertificateErrors)
        {
            var apiUrl = BuildApiUrl(zabbixUrl, zabbixApiEndpoint);

            var client = GetClient(ignoreCertificateErrors);

            // Zabbix-Severity ist 0..5. Die Anwendung behandelt alles unterhalb
            // WarningSeverityThreshold als ignoriert, deshalb schon serverseitig
            // nur die relevanten Severities abfragen.
            var clampedMinimumSeverity = Math.Clamp(minimumSeverity, 0, 5);
            var relevantSeverities = Enumerable.Range(
                clampedMinimumSeverity,
                6 - clampedMinimumSeverity
            ).ToArray();

            var requestObj = new
            {
                jsonrpc = "2.0",
                method = "problem.get",
                @params = new
                {
                    output = new[] { "eventid", "objectid", "name", "severity", "clock", "acknowledged", "opdata" },

                    // Dashboard-nahe Problemmenge:
                    // - nur Trigger-Probleme
                    // - nur ungelöste Probleme
                    // - unterdrückte Probleme (z. B. Wartung / manuelle Suppression) ausblenden
                    // - Symptom-Probleme nicht zusätzlich als eigene Zeile anzeigen
                    source = 0,
                    @object = 0,
                    recent = false,
                    suppressed = false,
                    symptom = false,

                    // problem.get unterstuetzt in Zabbix 7.4 offiziell den
                    // Parameter "severities". Dadurch werden z. B. Severity 0/1
                    // bei der Standardkonfiguration gar nicht erst geladen.
                    severities = relevantSeverities,

                    sortfield = new[] { "eventid" },
                    sortorder = "DESC"
                },
                id = 420 // id kann beliebig sein - für Zuordnung von Request und Response, weil aber immer nur einen Request gleichzeitig ist die konkrete Zahl egal
            };

            var json = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = content
            };

            // Authorization pro Request setzen (nicht in DefaultRequestHeaders) damit unterschiedliche Tokens/Clients sich nicht gegenseitig stören
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(responseJson);

            ThrowIfJsonRpcError(document.RootElement);

            // Fehler von API werden in "error" zurückgegeben, bei Erfolg in "result"
            // result von API in Liste von ZabbixProblem-Objekten umwandeln

            var problems = new List<ZabbixProblem>();

            if (!document.RootElement.TryGetProperty("result", out var result))
                return problems;

            foreach (var item in result.EnumerateArray())
            {
                var clockRaw = item.TryGetProperty("clock", out var clockElement)
                    ? clockElement.GetString() ?? "0"
                    : "0";

                var clock = long.TryParse(clockRaw, out var unixTime) ? unixTime : 0;

                problems.Add(new ZabbixProblem
                {
                    EventId = item.TryGetProperty("eventid", out var eventIdElement)
                        ? eventIdElement.GetString() ?? ""
                        : "",

                    // objectid ist bei Trigger-Problemen die TriggerId
                    TriggerId = item.TryGetProperty("objectid", out var objectIdElement)
                        ? objectIdElement.GetString() ?? ""
                        : "",

                    Name = item.TryGetProperty("name", out var nameElement)
                        ? nameElement.GetString() ?? ""
                        : "",

                    OperationalData = item.TryGetProperty("opdata", out var opDataElement)
                        ? opDataElement.GetString() ?? ""
                        : "",

                    Severity = item.TryGetProperty("severity", out var severityElement) &&
                               int.TryParse(severityElement.GetString(), out var severity)
                        ? severity
                        : 0, // severity ist eigentlich immer eine Zahl, aber sicherheitshalber TryParse

                    Time = DateTimeOffset.FromUnixTimeSeconds(clock).LocalDateTime,

                    Acknowledged = item.TryGetProperty("acknowledged", out var acknowledgedElement) &&
                                   acknowledgedElement.GetString() == "1"
                });
            }

            // problem.get kann noch offene Events liefern, deren Trigger/Host/Item inzwischen
            // deaktiviert wurde. Das Zabbix-Frontend blendet solche Probleme aus.
            // Deshalb zuerst auf aktuell überwachte Trigger filtern und erst danach
            // weitere Details für die verbleibenden Probleme laden.
            await FilterMonitoredTriggersAndAddDetailsAsync(client, apiUrl, apiToken, problems);
            await AddHostNamesToProblemsAsync(client, apiUrl, apiToken, problems);

            return problems;
        }

        // Holt die Hostnamen zu den Problem-EventIds nach
        // problem.get selbst liefert keine Hosts, event.get kann per selectHosts die zugehörigen Hosts zurückgeben
        private static async Task AddHostNamesToProblemsAsync(
            HttpClient client,
            string apiUrl,
            string apiToken,
            List<ZabbixProblem> problems)
        {
            var eventIds = problems
                .Select(p => p.EventId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (eventIds.Count == 0)
                return;

            var requestObj = new
            {
                jsonrpc = "2.0",
                method = "event.get",
                @params = new
                {
                    output = new[] { "eventid" },
                    eventids = eventIds,
                    selectHosts = new[] { "hostid", "host", "name" }
                },
                id = 421
            };

            var json = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = content
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(responseJson);

            ThrowIfJsonRpcError(document.RootElement);

            if (!document.RootElement.TryGetProperty("result", out var result))
                return;

            var hostNamesByEventId = new Dictionary<string, string>();

            foreach (var eventItem in result.EnumerateArray())
            {
                var eventId = eventItem.TryGetProperty("eventid", out var eventIdElement)
                    ? eventIdElement.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(eventId))
                    continue;

                if (!eventItem.TryGetProperty("hosts", out var hostsElement))
                    continue;

                if (hostsElement.ValueKind != JsonValueKind.Array)
                    continue;

                var hostNames = new List<string>();

                foreach (var host in hostsElement.EnumerateArray())
                {
                    var visibleName = host.TryGetProperty("name", out var nameElement)
                        ? nameElement.GetString()
                        : null;

                    var technicalName = host.TryGetProperty("host", out var hostElement)
                        ? hostElement.GetString()
                        : null;

                    var hostName = !string.IsNullOrWhiteSpace(visibleName)
                        ? visibleName
                        : technicalName;

                    if (!string.IsNullOrWhiteSpace(hostName))
                        hostNames.Add(hostName);
                }

                if (hostNames.Count > 0)
                    hostNamesByEventId[eventId] = string.Join(", ", hostNames.Distinct());
            }

            foreach (var problem in problems)
            {
                if (hostNamesByEventId.TryGetValue(problem.EventId, out var hostName))
                {
                    problem.HostName = hostName;
                }
            }
        }

        // Holt die zu den Problemen gehörenden Trigger nach und filtert dabei auf
        // aktuell überwachte Trigger. Zabbix 7.4 trigger.get monitored=true bedeutet:
        // Trigger enabled + Host monitored + alle verwendeten Items enabled.
        // Dadurch werden z. B. offene Alt-Probleme eines später deaktivierten Triggers
        // nicht mehr im Tray angezeigt, genau wie im Zabbix-Frontend.
        // Gleichzeitig werden Item-/Triggerdetails für die Anzeige nachgeladen.
        private static async Task FilterMonitoredTriggersAndAddDetailsAsync(
            HttpClient client,
            string apiUrl,
            string apiToken,
            List<ZabbixProblem> problems)
        {
            var triggerIds = problems
                .Select(p => p.TriggerId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (triggerIds.Count == 0)
                return;

            var requestObj = new
            {
                jsonrpc = "2.0",
                method = "trigger.get",
                @params = new
                {
                    output = new[] { "triggerid", "description", "event_name" },
                    triggerids = triggerIds,

                    // Laut Zabbix 7.4 API offiziell unterstützt:
                    // nur enabled Trigger auf monitored Hosts, die nur enabled Items verwenden.
                    monitored = true,

                    selectItems = new[] { "itemid", "name", "key_" }
                },
                id = 422
            };

            var json = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = content
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(responseJson);

            ThrowIfJsonRpcError(document.RootElement);

            if (!document.RootElement.TryGetProperty("result", out var result))
                return;

            var monitoredTriggerIds = new HashSet<string>();
            var monitoredObjectsByTriggerId = new Dictionary<string, string>();

            foreach (var trigger in result.EnumerateArray())
            {
                var triggerId = trigger.TryGetProperty("triggerid", out var triggerIdElement)
                    ? triggerIdElement.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(triggerId))
                    continue;

                // Jeder Trigger, den trigger.get mit monitored=true zurückliefert,
                // darf in der Tray-Problemliste bleiben.
                monitoredTriggerIds.Add(triggerId);

                if (!trigger.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
                    continue;

                var itemNames = new List<string>();

                foreach (var item in itemsElement.EnumerateArray())
                {
                    var itemName = item.TryGetProperty("name", out var itemNameElement)
                        ? itemNameElement.GetString()
                        : null;

                    var itemKey = item.TryGetProperty("key_", out var itemKeyElement)
                        ? itemKeyElement.GetString()
                        : null;

                    var displayName = !string.IsNullOrWhiteSpace(itemName)
                        ? itemName
                        : itemKey;

                    if (!string.IsNullOrWhiteSpace(displayName))
                        itemNames.Add(displayName);
                }

                if (itemNames.Count > 0)
                    monitoredObjectsByTriggerId[triggerId] = string.Join(", ", itemNames.Distinct().Take(2));
            }

            // problem.get kann ungelöste Alt-Probleme eines inzwischen deaktivierten
            // Triggers/Hosts/Items weiterhin liefern. Diese Trigger fehlen in der
            // monitored=true-Antwort und werden deshalb hier entfernt.
            problems.RemoveAll(problem =>
                string.IsNullOrWhiteSpace(problem.TriggerId) ||
                !monitoredTriggerIds.Contains(problem.TriggerId));

            foreach (var problem in problems)
            {
                if (monitoredObjectsByTriggerId.TryGetValue(problem.TriggerId, out var monitoredObject))
                {
                    problem.MonitoredObject = monitoredObject;
                }
            }
        }

        public async Task<string> GetVersionAsync(string zabbixUrl, string zabbixApiEndpoint, bool ignoreCertificateErrors)
        {
            var apiUrl = BuildApiUrl(zabbixUrl, zabbixApiEndpoint);

            var client = GetClient(ignoreCertificateErrors);

            var requestObj = new
            {
                jsonrpc = "2.0",
                method = "apiinfo.version",
                @params = new { },
                id = 1
            };

            var json = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = content
            };

            var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(responseJson);

            ThrowIfJsonRpcError(document.RootElement);

            if (document.RootElement.TryGetProperty("result", out var result))
                return result.GetString() ?? "";

            throw new Exception("Keine gültige Antwort vom Server erhalten");
        }

        private static string BuildApiUrl(string zabbixUrl, string zabbixApiEndpoint)
        {
            var endpoint = string.IsNullOrWhiteSpace(zabbixApiEndpoint)
                ? DefaultZabbixApiEndpoint
                : zabbixApiEndpoint.Trim();

            if (!endpoint.StartsWith("/"))
                endpoint = "/" + endpoint;

            return zabbixUrl.TrimEnd('/') + endpoint;
        }

        private static void ThrowIfJsonRpcError(JsonElement rootElement)
        {
            if (!rootElement.TryGetProperty("error", out var error))
                return;

            var message = error.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : null;

            var data = error.TryGetProperty("data", out var dataElement)
                ? dataElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(data))
                throw new Exception($"Zabbix API Fehler: {message} - {data}");

            throw new Exception($"Zabbix API Fehler: {message ?? "Unbekannter Fehler"}");
        }
    }
}
