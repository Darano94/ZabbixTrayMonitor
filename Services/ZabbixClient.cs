using System.Net.Http;
using System.Text;
using System.Text.Json;

// Kommunikation mit der Zabbix API

namespace ZabbixTrayMonitor.Services
{
    public class ZabbixClient
    {
        private const string ZabbixApiEndpoint = "/api_jsonrpc.php";
        public async Task<string> GetVersionAsync(string zabbixUrl, bool ignoreCertificateErrors)
        {
            var apiUrl = zabbixUrl.TrimEnd('/') + "/api_jsonrpc.php";

            var handler = new HttpClientHandler();

            if (ignoreCertificateErrors)
            {
                // Akzeptiere auch ungültige/self-signed SSL-Zertifikate
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            using var httpClient = new HttpClient(handler);

            // Zabbix nutzt json-rpc deswegen das objekt
            var request = new
            {
                jsonrpc = "2.0",
                method = "apiinfo.version",
                @params = new { },
                id = 1
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(apiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(responseJson);

            if (document.RootElement.TryGetProperty("result", out var result))
                return result.GetString() ?? "";

            throw new Exception("Keine gültige Zabbix-Antwort erhalten.");
        }
    }
}