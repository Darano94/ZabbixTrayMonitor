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
        private readonly string _configFilename = "config.json";
        private readonly string _configFoldername = "ZabbixTrayMonitor";

        public ConfigService()
        {
            _appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                _configFoldername
            );

            _configPath = Path.Combine(_appFolder, _configFilename);
        }

        public bool ConfigExists()
        {
            return File.Exists(_configPath);
        }

        public ZabbixConfig Load()
        {
            if (!ConfigExists())
                return new ZabbixConfig();

            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<ZabbixConfig>(json) ?? new ZabbixConfig();
        }

        public void Save(ZabbixConfig config)
        {
            Directory.CreateDirectory(_appFolder);

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_configPath, json);
        }

        public string GetConfigPath()
        {
            return _configPath;
        }
    }
}
