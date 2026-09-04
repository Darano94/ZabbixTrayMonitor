# Zabbix Tray Monitor

Kleiner Windows Tray Client fuer Zabbix.

Die Anwendung laeuft im Windows System Tray und zeigt den aktuellen Zabbix-Status ueber ein farbiges Tray-Icon, einen Tooltip und ein kleines Problemfenster an.

Je nach Zustand zeigt das Tray-Icon direkt an, ob alles fehlerfrei ist, Warnungen vorhanden sind oder Fehler vorliegen. Tooltip und Problemfenster listen die aktuellen relevanten Zabbix-Probleme auf.

## Funktionen

- Tray-Status fuer OK, Warnung, Fehler und unbekannten Zustand
- Automatische Aktualisierung ueber konfigurierbares Abfrageintervall
- Kompakter Tooltip mit den wichtigsten aktuellen Problemen
- Problemfenster mit Host, Zeit, Meldung und ueberwachtem Objekt
- Dark- und Light-Mode
- API-Token im Windows Credential Manager
- Alarmbearbeitung direkt im Problemfenster:
  - Nur bestaetigen
  - Fuer 5 Minuten unterdruecken
  - Fuer 15 Minuten unterdruecken
  - Fuer 1 Stunde unterdruecken
  - Fuer 3 Stunden unterdruecken
  - Fuer 1 Tag unterdruecken
  - Unterdruecken bis zu einem frei waehlbaren Datum/Zeitpunkt
- Konfigurierbare Standardnachricht fuer Bestaetigungen/Unterdrueckungen
- Nachricht kann pro Aktion direkt im Kontextmenue ueberschrieben werden
- Unterdrueckte Probleme koennen wahlweise weiterhin angezeigt werden
- Optionaler automatischer Refresh nach einer Alarmaktion

## Download

[![Download](https://img.shields.io/badge/Download-Releases-blue)](https://github.com/Darano94/ZabbixTrayMonitor/releases)

## Screenshots

### Tray Tooltip ohne Probleme

<img src="Assets/README/tray-tooltip-ok.png" alt="Tray Tooltip ohne Probleme">

### Tray Tooltip mit Problemen

<img src="Assets/README/tray-tooltip-problem.png" alt="Tray Tooltip mit Problemen">

### Problemfenster

<img src="Assets/README/problems-window.png" alt="Problemfenster" width="290">

### Alarmbearbeitung

<img src="Assets/README/alarm-editing.png" alt="Alarmbearbeitung" width="420">

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
  "ShowSuppressedProblems": false,
  "RefreshAfterProblemAction": true,
  "AcknowledgeMessage": "Bestätigt von max.mustermann",
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

Der Token wird im Windows Credential Manager gespeichert.

Beispiel:

```text
ZabbixTrayMonitor.ApiToken
```

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

## Alarmbearbeitung

Ein Rechtsklick auf ein Problem oeffnet das Menue `Alarm bearbeiten`.

`Nur bestaetigen...` fuehrt in Zabbix `event.acknowledge` mit Bestaetigung und Nachricht aus.

Die zeitlich begrenzten Unterdrueckungen bestaetigen das Event, fuegen die Nachricht hinzu und setzen eine Unterdrueckung bis zum gewaehlten Zeitpunkt.

Wenn `ShowSuppressedProblems` auf `false` steht, werden unterdrueckte Probleme aus Tray-Status, Tooltip und Problemfenster ausgeblendet.

## API

Die Anwendung nutzt die Zabbix JSON-RPC API.

Standard API-Pfad:

```text
/api_jsonrpc.php
```

Authentifizierung erfolgt ueber Bearer Token.

Fuer die Problemliste werden die aktuellen Probleme gesammelt abgefragt und mit Hostnamen sowie Trigger-/Item-Informationen angereichert. Es wird nicht pro Problem ein einzelner API-Request ausgefuehrt.

Genutzte Zabbix API-Methoden:

```text
problem.get
event.get
trigger.get
event.acknowledge
apiinfo.version
```

Der API Token braucht die benoetigten Rechte fuer diese Methoden. Fuer die Alarmbearbeitung muessen ausserdem die Zabbix-Berechtigungen des Token-Benutzers das Bestaetigen bzw. Unterdruecken der betreffenden Events erlauben.

## Bibliotheken

### Hardcodet.NotifyIcon.Wpf

WPF-Unterstuetzung fuer Windows System Tray Icons.

### CredentialManagement

Zugriff auf den Windows Credential Manager.
