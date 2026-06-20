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
        private const string ZabbixApiEndpoint = "/api_jsonrpc.php";

        // Wiederverwendete HttpClient-Instanzen: eine standard, eine mit zertbypass
        private static readonly HttpClient _defaultClient = new HttpClient();
        private static readonly HttpClient _insecureClient;

        static ZabbixClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            _insecureClient = new HttpClient(handler);
        }

        private static HttpClient GetClient(bool ignoreCertificateErrors)
        {
            return ignoreCertificateErrors ? _insecureClient : _defaultClient;
        }

        public async Task<List<ZabbixProblem>> GetProblemsAsync(string zabbixUrl, string apiToken, bool ignoreCertificateErrors)
        {
            var apiUrl = zabbixUrl.TrimEnd('/') + ZabbixApiEndpoint;

            var client = GetClient(ignoreCertificateErrors);

            var requestObj = new
            {
                jsonrpc = "2.0",
                method = "problem.get",
                @params = new
                {
                    output = new[] { "eventid", "name", "severity", "clock", "acknowledged" },
                    sortfield = new[] { "eventid" },
                    sortorder = "DESC"
                },
                id = 2
            };

            var json = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = content
            };

            // Authorization pro Request setzen (nicht in DefaultRequestHeaders), damit unterschiedliche Tokens/Clients sich nicht gegenseitig stören
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseJson);

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.GetProperty("message").GetString();
                var data = error.TryGetProperty("data", out var errorData)
                    ? errorData.GetString()
                    : "";

                throw new Exception($"{message}: {data}");
            }

            var problems = new List<ZabbixProblem>();

            if (!document.RootElement.TryGetProperty("result", out var result))
                return problems;

            foreach (var item in result.EnumerateArray())
            {
                var clockRaw = item.GetProperty("clock").GetString() ?? "0";
                var clock = long.TryParse(clockRaw, out var unixTime) ? unixTime : 0;

                problems.Add(new ZabbixProblem
                {
                    EventId = item.GetProperty("eventid").GetString() ?? "",
                    Name = item.GetProperty("name").GetString() ?? "",
                    Severity = int.TryParse(item.GetProperty("severity").GetString(), out var severity) ? severity : 0,
                    Time = DateTimeOffset.FromUnixTimeSeconds(clock).LocalDateTime,
                    Acknowledged = item.GetProperty("acknowledged").GetString() == "1"
                });
            }

            return problems;
        }

        public async Task<string> GetVersionAsync(string zabbixUrl, bool ignoreCertificateErrors)
        {
            var apiUrl = zabbixUrl.TrimEnd('/') + ZabbixApiEndpoint;

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

            if (document.RootElement.TryGetProperty("result", out var result))
                return result.GetString() ?? "";

            throw new Exception("Keine gültige Antwort vom Server erhalten");
        }
    }
}