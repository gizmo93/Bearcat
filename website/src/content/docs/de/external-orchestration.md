---
title: "Bearcat mit externen Tools steuern"
description: "Erstelle Releases, steuere Uploads und erfasse Forenposts über die REST-API."
---

Über die [REST-API](/Bearcat/de/rest-api/) erstellst du Releases, steuerst Uploads und erfasst Forenposts.
Alle Parameter und Antwortfelder stehen in der [API-Referenz](/Bearcat/api/).

## Befehle

| Endpunkt | Zweck |
| --- | --- |
| `POST /api/v1/releases` | Erstellt ein Release aus einem Ordner und einem Releasetemplate. |
| `POST /api/v1/uploads/{uploadId}/reupload` | Erstellt einen neuen Upload, wenn der bisherige `Offline`, `PartiallyOnline`, `Canceled` oder `Failed` ist. |
| `POST /api/v1/uploads/{uploadId}/cancel` | Bricht einen anstehenden oder laufenden Upload ab. |
| `POST /api/v1/uploads/{uploadId}/resume` | Stellt einen abgebrochenen Upload erneut in die Warteschlange. |
| `POST /api/v1/uploads/{uploadId}/check-state` | Prüft sofort den Onlinestatus beim Hoster. |
| `POST /api/v1/releases/{releaseId}/posted-locations` | Speichert die URL, unter der das Release gepostet wurde. |
| `POST /api/v1/releases/{releaseId}/mark-posted` | Entfernt das Release aus der [Postwarteschlange](/Bearcat/de/post-queue/). |

Die API kann keine Hoster, Templates, Regeln oder Zugangsdaten ändern und keine Daten löschen.

## API-Schlüssel

Setze `Bearcat:ApiKey` und sende den Wert bei jeder `POST`-Anfrage im Header `X-Api-Key` mit.
Ohne konfigurierten Schlüssel geben Befehle `403` zurück. `GET`-Anfragen brauchen keinen Schlüssel.

Schlüssel erzeugen:

```bash
openssl rand -hex 32
```

Starte Bearcat neu, nachdem du ihn gesetzt hast.

### Docker

Setze `BEARCAT_API_KEY` in `.env`:

```text
BEARCAT_API_KEY=your-key
```

Führe dann `docker compose up -d` aus.

### Windows-Dienst

Füge in `%ProgramData%\Bearcat\config.json` einen Abschnitt `Bearcat` hinzu:

```json
{
  "Database": { "Provider": "Sqlite", "SqliteFilePath": "..." },
  "Bearcat": {
    "ApiKey": "your-key"
  }
}
```

Starte den Dienst mit `sc stop Bearcat` und `sc start Bearcat` neu.

`Bearcat.Cli.exe setup` und `Bearcat.Cli.exe set-db-password` behalten den Abschnitt bei. Siehe [Bearcat als Windows-Dienst ausführen](/Bearcat/de/use-the-windows-service/#speicherort-der-konfiguration).

### Desktopanwendung

Gib den Schlüssel im Feld **API key** der [Desktopanwendung](/Bearcat/de/use-the-desktop-launcher/) ein und klicke auf **Save**. Läuft Bearcat, klicke auf **Stop** und **Start Bearcat**.

### Direkt gestarteter Bearcat.Host

Setze die Umgebungsvariable `Bearcat__ApiKey` oder erstelle `appsettings.user.json` im Arbeitsverzeichnis:

```json
{
  "Bearcat": {
    "ApiKey": "your-key"
  }
}
```

### Netzwerk

Betreibe Bearcat in einem vertrauenswürdigen Netzwerk. Der Schlüssel schützt Befehle, aber jeder,
der Bearcat erreichen kann, kann Releases, Uploads und Downloadlinks lesen.

Die Desktopanwendung und der Windows-Dienst lauschen nur auf `127.0.0.1:17208`, das aufrufende Tool muss also auf demselben Rechner laufen. Docker veröffentlicht Port `8080`.

## Beispielablauf

```bash
BEARCAT=http://localhost:8080
API_KEY=your-key
```

### 1. Release erstellen

```bash
curl -X POST "$BEARCAT/api/v1/releases" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"folderPath": "/mnt/data/releases/Some.Release.2026.1080p", "releaseTemplateId": 3}'
```

- `folderPath`: absoluter Pfad auf dem Bearcat-Host. In Docker verwendest du den Containerpfad unter `/mnt/data/releases`, wo `RELEASES_DIR` eingebunden ist.
- `releaseTemplateId`: die Zahl am Ende der URL des Templates unter **Releasetemplates**.
- `name`: optional, standardmässig der Ordnername.
- `primaryLanguageCode`: optional, zwei Buchstaben, zum Beispiel `de`.

Warte mit dem Aufruf, bis alle Dateien im Ordner sind. Bearcat wartet nicht, bis Dateien fertig
kopiert sind. Die Anfrage kann einige Sekunden dauern, während Bearcat die Releaseinformationen abruft.

Antwort: `201 Created` mit dem Release.

### 2. Uploads beobachten

Bearcat erstellt Archive und Uploads im Hintergrund nach dem [Cooldown für initiale Uploads](/Bearcat/de/advanced-configuration/#cooldown-für-initiale-uploads).

Uploads eines Releases:

```bash
curl "$BEARCAT/api/v1/uploads?releaseId=42"
```

Mögliche Werte für `uploadState`: `WaitingForArchive`, `Pending`, `Uploading`, `Completed`, `Failed`, `CancellationRequested`, `Canceled`.

Abgeschlossene Uploads vom ältesten zum neuesten abfragen:

```bash
curl -G "$BEARCAT/api/v1/uploads" \
  --data-urlencode "uploadedAfter=$LAST_UPLOADED_AT" \
  --data-urlencode "pageSize=100"
```

Übergib beim nächsten Aufruf den `uploadedAt`-Wert des letzten Eintrags als `uploadedAfter`. Verwende `--data-urlencode`, damit Zeichen wie `+` im Zeitstempel korrekt übertragen werden.

### 3. Releases zum Posten finden

```bash
curl "$BEARCAT/api/v1/releases?inPostQueue=true&pageSize=100"
```

Liefert dieselben Releases wie die [Postwarteschlange](/Bearcat/de/post-queue/).

### 4. Links abrufen

```bash
curl "$BEARCAT/api/v1/releases/42/uploads"
curl "$BEARCAT/api/v1/releases/42/uploads/1234/links?pageSize=100"
curl "$BEARCAT/api/v1/releases/42/uploads/1234/linkcrypter-links"
```

### 5. Post erfassen

```bash
curl -X POST "$BEARCAT/api/v1/releases/42/posted-locations" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"url": "https://forum.example.com/threads/12345"}'
```

`distributionSiteRegistrationId` und `forumPostTemplateId` sind optional.

Antwort: `201 Created`, oder `200 OK` mit dem bestehenden Eintrag, wenn die URL für dieses Release bereits gespeichert ist.

```bash
curl -X POST "$BEARCAT/api/v1/releases/42/mark-posted" -H "X-Api-Key: $API_KEY"
```

Antwort: `204 No Content`. Das Release kommt zurück in die Postwarteschlange, wenn ein neuerer Upload abgeschlossen wird.

## Statuscodes

| Code | Bedeutung |
| --- | --- |
| `400` | Ungültige Anfrage, zum Beispiel ein relativer `folderPath`, ein fehlender Ordner oder ein unbekanntes Template. `errors` listet die Felder. |
| `401` | `X-Api-Key` fehlt oder ist falsch. |
| `403` | Kein API-Schlüssel konfiguriert. |
| `404` | Release oder Upload nicht gefunden. |
| `409` | Der Befehl ist im aktuellen Zustand nicht erlaubt, etwa weil der Ordner bereits verwendet wird oder der Upload nicht abgebrochen ist. |

Fehlerantworten verwenden das JSON-Format Problem Details. Die Fehlermeldung steht in `detail`.
