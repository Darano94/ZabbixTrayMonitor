# Zabbix Tray Monitor

Kleiner Windows Tray Client für Zabbix

## Config

`%AppData%\ZabbixTrayMonitor\config.json`
{
  "ZabbixUrl": "https://zabbix.example.local",
  "PollIntervalSeconds": 60,
  "IgnoreCertificateErrors": false,
  "WarningSeverityThreshold": 2,
  "ErrorSeverityThreshold": 4
}

## Severity Mapping

Zabbis API liefert Severity Werte zwischen 0 und 5

0 = Not classified
1 = Information
2 = Warning
3 = Average
4 = High
5 = Disaster

WarningSeverityThreshold bestimmt ab wann ein Severity Wert als Warnung gewertet wird.

ErrorSeverityThreshold bestimmt ab wann ein Severity Wert als Fehler gewertet wird.

Standardmäßig:

- Severity 0-1 -> ignoriert
- Severity 2-3 -> Warnung
- Severity 4-5 -> Fehler

## API

Bearer Token

## Bibliotheken

Hardcodet.NotifyIcon.Wpf
- WPF Unterstützung für Windows System Tray Icons

CredentialManagement
- Zugriff auf den Windows Credential Manager