using CredentialManagement;

// Verwaltet den Zabbix-User-Token im Windows-CredentialManager

namespace ZabbixTrayMonitor.Services
{
    public class CredentialService
    {
        private const string CredentialTarget = "ZabbixTrayMonitor.ApiToken";
        private const string CredentialUsername = "api-token";

        public void SaveToken(string token)
        {
            using var credential = new Credential
            {
                Target = CredentialTarget,
                Username = CredentialUsername,
                Password = token,
                Type = CredentialType.Generic,
                PersistanceType = PersistanceType.LocalComputer
            };

            credential.Save();
        }

        public string? GetToken()
        {
            using var credential = new Credential
            {
                Target = CredentialTarget,
                Type = CredentialType.Generic
            };

            return credential.Load() ? credential.Password : null;
        }

        public bool HasToken()
        {
            return !string.IsNullOrWhiteSpace(GetToken());
        }

        public void DeleteToken()
        {
            using var credential = new Credential
            {
                Target = CredentialTarget,
                Type = CredentialType.Generic
            };

            credential.Delete();
        }
    }
}