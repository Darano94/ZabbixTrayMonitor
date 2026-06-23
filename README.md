# Zabbix Tray Monitor

Kleiner Windows Tray Client fuer Zabbix.

Die Anwendung laeuft im Windows System Tray und zeigt den aktuellen Zabbix-Status ueber ein farbiges Tray-Icon, einen Tooltip und ein kleines Problemfenster an.

Je nach Zustand zeigt das Tray-Icon direkt an, ob alles fehlerfrei ist, Warnungen vorhanden sind oder Fehler vorliegen. Tooltip und Problemfenster listen die aktuellen relevanten Zabbix-Probleme auf.

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
  "CredentialUsername": "api-token"
}
```

Der API Token wird nicht in der `config.json` gespeichert.

Der Token wird im Windows Credential Manager gespeichert. Das Credential Target wird aus `AppName` und `CredentialTargetSuffix` gebaut.

Beispiel:

```text
ZabbixTrayMonitor.ApiToken
```


Der Token braucht die benötigten Reche auf die folgenden Zabbix-Endpunkte:
- problem.get
- event.get
- trigger.get
- apiinfo.version

## Severity Mapping

Die Zabbix API liefert Severity-Werte zwischen 0 und 5:

```text
0 = Not classified
1 = Information
2 = Warning
3 = Average
4 = High
5 = Disaster
```

`WarningSeverityThreshold` bestimmt, ab welchem Severity-Wert ein Problem als Warnung gewertet wird.

`ErrorSeverityThreshold` bestimmt, ab welchem Severity-Wert ein Problem als Fehler gewertet wird.

Standardmaessig:

```text
Severity 0-1 -> ignoriert
Severity 2-3 -> Warnung
Severity 4-5 -> Fehler
```

## API

Die Anwendung nutzt die Zabbix JSON-RPC API

Standard API-Pfad:

```text
/api_jsonrpc.php
```

Authentifizierung erfolgt ueber Bearer Token.

Fuer die Problemliste werden die aktuellen Probleme abgefragt und mit weiteren Informationen wie Hostnamen und Trigger-/Item-Informationen angereichert.

Genutzte Zabbix API-Methoden:

```text
problem.get
event.get
trigger.get
apiinfo.version
```

## Bibliotheken

### Hardcodet.NotifyIcon.Wpf

WPF-Unterstuetzung fuer Windows System Tray Icons.

### CredentialManagement

Zugriff auf den Windows Credential Manager.

## Todo

* Probleme acknowledgen
