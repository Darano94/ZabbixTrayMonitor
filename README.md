# Zabbix Tray Monitor

Kleiner Windows Tray Client für Zabbix

Die Anwendung läuft im Windows System Tray und zeigt den aktuellen Zabbix-Status über ein farbiges Tray-Icon an. 
Je nach Zustand wird sichtbar, ob es Fehler oder Warnungen gibt. 

Über das Kontextmenü können weitere Informationen abgerufen und die Zabbix-Weboberfläche geöffnet werden.

## Download

[![Download](https://img.shields.io/badge/Download-Releases-blue)](https://github.com/Darano94/ZabbixTrayMonitor/releases)

## Screenshots

### Tray Tooltip ohne Probleme

<img src="Assets/README/tray-tooltip-ok.png" alt="Tray Tooltip ohne Probleme">

### Tray Tooltip mit Problemen

<img src="Assets/README/tray-tooltip-problem.png" alt="Tray Tooltip mit Problemen">

### Problemfenster

<img src="Assets/README/problems-window.png" alt="Problemfenster" width="290">

### Einstellungen

<img src="Assets/README/settings-window.png" alt="Einstellungen" width="420">

## Basisfunktionen

* Windows Tray Icon mit farbiger Statusanzeige
* automatische Abfrage der aktuellen Zabbix-Probleme
* Tooltip mit Fehlern und Warnungen
* Problemfenster mit aktueller Problemliste
* konfigurierbares Abfrageintervall
* konfigurierbare Severity-Schwellenwerte
* konfigurierbare Statusfarben
* Dark Mode / Light Mode
* direkter Link zum Zabbix Dashboard
* Zabbix API Token wird im Windows Credential Manager gespeichert

## Config

Die Konfiguration liegt unter:

```text
%AppData%\ZabbixTrayMonitor\config.json
```

Beispiel:

```json
{
  "ZabbixUrl": "https://zabbix.home",
  "ZabbixDashboardUrl": "https://zabbix.home/zabbix.php?action=dashboard.view&dashboardid=407",
  "ZabbixApiEndpoint": "/api_jsonrpc.php",
  "PollIntervalSeconds": 60,
  "IgnoreCertificateErrors": true,
  "UseDarkMode": true,
  "AppName": "ZabbixTrayMonitor",
  "WarningSeverityThreshold": 2,
  "ErrorSeverityThreshold": 4,
  "StatusColorError": "#FF0015",
  "StatusColorWarning": "#F3C601",
  "StatusColorInfo": "#808080",
  "CredentialTargetSuffix": "ApiToken",
  "CredentialUsername": "api-token-username"
}
```

Der Zabbix-Api-Token wird im Windows Credential Manager gespeichert
Das Credential Target wird aus `AppName` und `CredentialTargetSuffix` gebaut

Beispiel:

```text
ZabbixTrayMonitor.ApiToken
```

## Severity Mapping

Die Zabbix API returned Severity-Werte zwischen 0 und 5:

```text
0 = Not classified
1 = Information
2 = Warning
3 = Average
4 = High
5 = Disaster
```

`WarningSeverityThreshold` bestimmt ab welchem Wert ein Problem als Warnung gilt

`ErrorSeverityThreshold` bestimmt ab welchem Wert ein Problem als Fehler gilt

Standardmaessig:

```text
Severity 0-1 -> ignoriert
Severity 2-3 -> Warnung
Severity 4-5 -> Fehler

## API

Die Anwendung nutzt die Zabbix JSON-RPC API mit Bearer Token Authentifizierung

Standard API-Pfad:

```text
/api_jsonrpc.php
```

## Bibliotheken

### Hardcodet.NotifyIcon.Wpf

WPF-Unterstuetzung fuer Windows System Tray Icons

### CredentialManagement

Zugriff auf Windows Credential Manager

## Todo

* Probleme acknowledgen
* echte Hostnamen zu Problemen anzeigen (Endpunkt trigger.get)
