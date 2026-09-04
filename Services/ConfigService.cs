using System;
using System.IO;
using System.Text.Json;
using ZabbixTrayMonitor.Models;

// Lädt und speichert die Einstellungen als JSON in %appdata%

namespace ZabbixTrayMonitor.Services
{
    public class ConfigService
    {
        private readonly string _appFolder;
        private readonly string _configPath;
        private const string ConfigFilename = "config.json";

        // konstanter AppData-Ordnername (nicht abhängig von konfigurierbarem AppName) .. Todo
        private const string AppDataBaseName = "ZabbixTrayMonitor";

        // formatiert JSON mit Zeilenumbrüchen und Einrückungen sonst ist es ein langer oneliner
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public ConfigService()
        {
            _appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppDataBaseName
            );

            _configPath = Path.Combine(_appFolder, ConfigFilename);
        }

        public bool ConfigExists()
        {
            return File.Exists(_configPath);
        }

        public ZabbixConfig Load()
        {
            if (!ConfigExists())
                return new ZabbixConfig();

            try
            {
                var json = File.ReadAllText(_configPath);

                // wandelt JSON in ZabbixConfig-Objekt um, falls ungültig wird ein neues Objekt zurückgegeben
                var config = JsonSerializer.Deserialize<ZabbixConfig>(json, _jsonOptions) ?? new ZabbixConfig();

                // Migration des bisherigen Standardtexts. Eigene konfigurierte Nachrichten bleiben erhalten.
                // Den normalisierten Wert direkt speichern, damit der Legacy-Default nicht bei jedem Start
                // erneut aus der bestehenden config.json geladen wird.
                var resolvedAcknowledgeMessage =
                    ZabbixConfig.ResolveAcknowledgeMessage(config.AcknowledgeMessage);

                if (!string.Equals(
                        config.AcknowledgeMessage,
                        resolvedAcknowledgeMessage,
                        StringComparison.Ordinal))
                {
                    config.AcknowledgeMessage = resolvedAcknowledgeMessage;
                    Save(config);
                }

                return config;
            }
            catch
            {
                return new ZabbixConfig();
            }
        }

        public void Save(ZabbixConfig config)
        {
            Directory.CreateDirectory(_appFolder);

            var json = JsonSerializer.Serialize(config, _jsonOptions);

            File.WriteAllText(_configPath, json);
        }

        public string GetConfigPath()
        {
            return _configPath;
        }
    }
}
