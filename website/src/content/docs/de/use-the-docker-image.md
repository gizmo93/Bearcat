---
title: "Bearcat in Docker ausführen"
description: "Starte Bearcat mit Docker Compose und SQLite auf einem Linux-Rechner, NAS oder Server."
prev:
  label: "Installation wählen"
  link: "/Bearcat/de/#erste-schritte"
next:
  label: "Dein erster Upload"
  link: "/Bearcat/de/post-installation/"
---

Das Docker-Image enthält RAR und 7-Zip und verwendet **SQLite**. Du brauchst daher nur einen Container.
Du brauchst Docker mit Docker Compose. Das Image unterstützt nur `linux/amd64`. Raspberry Pi wird nicht unterstützt.

Unter Windows oder auf einem Mac mit Apple Silicon kannst du die
[native Desktopanwendung](/Bearcat/de/use-the-desktop-launcher/) verwenden.

## 0. Docker installieren

### Synology NAS
Installiere **Container Manager** über das Paket-Zentrum.

### Windows
Installiere [Docker Desktop](https://www.docker.com/products/docker-desktop/) oder [Rancher Desktop](https://rancherdesktop.io).

### Linux
Unter Linux empfehle ich [Docker Engine](https://docs.docker.com/engine/install/), auch auf Rechnern mit grafischer Oberfläche.
Sie läuft direkt auf dem Host und vermeidet die zusätzliche Dateifreigabe zwischen Host und VM.

### macOS
Du kannst Docker Desktop oder Rancher Desktop verwenden. Unter macOS empfehle ich
[OrbStack](https://orbstack.dev), besonders wegen der schnellen Dateizugriffe über Bind Mounts.

## 1. Compose-Datei herunterladen

Erstelle einen Ordner und lade diese Dateien über **Download raw file** auf GitHub herunter:

- [docker-compose.yml](https://github.com/gizmo93/Bearcat/blob/main/docker-compose.yml)
- [.env.example](https://github.com/gizmo93/Bearcat/blob/main/.env.example), umbenannt in `.env`

## 2. Releaseordner festlegen

Öffne `.env` in einem Texteditor und setze `RELEASES_DIR` auf einen vorhandenen Ordner mit deinen
Releasedateien. Bearcat braucht Lese- und Schreibzugriff darauf. Zum Beispiel:

```text
RELEASES_DIR=/srv/releases
BEARCAT_DATA_DIR=./bearcat-data
```

Ersetze `/srv/releases` durch deinen eigenen Pfad. Die übrigen Einstellungen kannst du unverändert lassen.
Docker legt einen fehlenden Releaseordner nicht an. Existiert der Ordner nicht, startet der Container nicht.

`BEARCAT_DATA_DIR` speichert die Datenbank und den Verschlüsselungsschlüssel, standardmässig in
`bearcat-data` neben deiner Compose-Datei. Lege diesen Datenordner auf lokalen Speicher.
Releasedateien können auf einer NFS- oder SMB-Freigabe liegen.

Die folgenden Einstellungen gelten nur für Docker Engine direkt auf Linux. Mit Docker Desktop
auf Windows, macOS oder Linux behältst du den normalen Bindmount aus der Compose-Datei.

Mit Docker Engine kann `rslave` Mountänderungen innerhalb von `RELEASES_DIR` vom Host an den Container weitergeben.
Der Hostmount muss dafür `shared` sein. Wird `RELEASES_DIR` selbst neu eingebunden, kann es nötig sein,
den Container neu zu erstellen. Details unter
[Bindpropagation in Docker](https://docs.docker.com/engine/storage/bind-mounts/#configure-bind-propagation).

Ergänze in `docker-compose.yml` unter `services.bearcat.volumes` im Abschnitt `bind` des Eintrags für
`${RELEASES_DIR}` die Zeile `propagation: rslave`. Lass den Eintrag für den Datenordner unverändert.

```yaml
- type: bind
  source: ${RELEASES_DIR:?Set RELEASES_DIR in .env to your release folder}
  target: /mnt/data/releases
  bind:
    create_host_path: false
    propagation: rslave
```

## 3. Bearcat starten

Öffne ein Terminal im Ordner mit `docker-compose.yml` und führe aus:

```bash
docker compose up -d
```

Öffne [http://localhost:8080](http://localhost:8080). Läuft Docker auf einem anderen Rechner oder einem NAS,
öffne stattdessen `http://<server-address>:8080`. Der erste Start kann einen Moment dauern.

**Weiter: [Hosterkonto hinzufügen und erstes Release hochladen](/Bearcat/de/post-installation/).**

In Bearcats Ordnerauswahl erscheint dein Releaseordner als `/mnt/data/releases`.

## Datenbank

Behalte SQLite oder richte mit den folgenden Schritten PostgreSQL ein.

### Stattdessen PostgreSQL verwenden

1. Speichere [docker-compose.postgres.yml](https://github.com/gizmo93/Bearcat/blob/main/docker-compose.postgres.yml) neben deiner Compose-Datei.
2. Setze in `.env` den Wert `POSTGRES_PASSWORD` auf ein eigenes Passwort.
3. Füge diese Zeile in `.env` ein, damit alle Compose-Befehle PostgreSQL verwenden:

```text
COMPOSE_FILE=docker-compose.yml:docker-compose.postgres.yml
```

Unter Windows trennst du die Dateinamen mit `;` statt `:`.
Mit `docker compose up -d` startest du PostgreSQL in einem zweiten Container und verbindest Bearcat damit.

PostgreSQL speichert seine Daten in `POSTGRES_DATA_DIR`, standardmässig `./postgres-data`.
Bearcat braucht `BEARCAT_DATA_DIR` weiterhin für seinen Verschlüsselungsschlüssel.
Ein Datenbankwechsel überträgt keine Daten; die neue Datenbank startet leer.

## Bearcat aktualisieren

Mit dem Standardtag `latest` aktualisierst du Bearcat mit diesen Befehlen im Ordner deiner Compose-Datei:

```bash
docker compose pull
docker compose up -d
```

Docker erstellt den Container neu, wenn sich das Image geändert hat. Behalte die Datenordner auf
deinem Host; sie enthalten die Datenbank und den Verschlüsselungsschlüssel.

### Bestehende Docker-Installationen aktualisieren

Behalte deine bestehenden Compose-Dateien, um PostgreSQL weiterzuverwenden. Ersetzt du sie durch
die aktuellen Dateien mit SQLite als Standard, füge diese Zeile in `.env` ein (Windows: `;` statt `:`):

```text
COMPOSE_FILE=docker-compose.yml:docker-compose.postgres.yml
```

Oder starte mit beiden Dateien:

```bash
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d
```

Ohne den Override startet Bearcat mit einer leeren SQLite-Datenbank. Deine PostgreSQL-Daten sind
weiterhin vorhanden. Füge den Override hinzu und führe `docker compose up -d` erneut aus, um zurückzuwechseln.

## Backups

Stoppe Bearcat vor dem Kopieren der SQLite-Datenbank:

```bash
docker compose stop
```

Kopiere `BEARCAT_DATA_DIR` (normalerweise `bearcat-data`) samt `bearcat.db` und `bearcat.key`. Starte Bearcat danach wieder:

```bash
docker compose start
```

Für ein Backup bei laufendem Bearcat verwende auf dem Host `sqlite3 bearcat-data/bearcat.db ".backup bearcat-backup.db"`.
Das Kopieren der verwendeten Datenbankdatei ergibt kein konsistentes Backup.

Bei PostgreSQL stoppst du beide Container, kopierst `BEARCAT_DATA_DIR` und `POSTGRES_DATA_DIR` und
startest sie wieder. Sichere Releasedateien separat.

Bewahre `bearcat.key` zusammen mit deinem Datenbankbackup auf. Ohne diese Datei kann Bearcat deine gespeicherten Kontozugangsdaten nicht lesen.

## Docker-Einstellungen

| Variable | Zweck |
| --- | --- |
| `RELEASES_DIR` | Verzeichnis auf dem Host mit deinen Releasedateien. Erforderlich. |
| `BEARCAT_DATA_DIR` | Datenbank (`bearcat.db` bei SQLite) und Verschlüsselungsschlüssel (`bearcat.key`). |
| `BEARCAT_PORT` | Port der Weboberfläche, normalerweise `8080`. |
| `BEARCAT_API_KEY` | Schlüssel für die Befehlsendpunkte der REST-API. Leer deaktiviert sie. Siehe [Bearcat mit externen Tools steuern](/Bearcat/de/external-orchestration/#api-schlüssel). |
| `BEARCAT_TIMEZONE` | Zeitzone für angezeigte Zeiten, zum Beispiel `Europe/Berlin`. Leer verwendet UTC. Siehe [Zeitzone](/Bearcat/de/advanced-configuration/#zeitzone). |
| `BEARCAT_IMAGE` | Image, standardmässig `ghcr.io/gizmo93/bearcat:latest`. Trage hier einen Versionstag oder ein eigenes Image ein. |
| `BEARCAT_CPU_LIMIT` | CPU-Limit in Kernen, zum Beispiel `0.5` für einen halben Kern. |
| `BEARCAT_MEMORY_LIMIT` | Speicherlimit, zum Beispiel `512m` oder `1g`. |

### PostgreSQL-Einstellungen

Diese Werte gelten nur mit `docker-compose.postgres.yml`.

| Variable | Zweck |
| --- | --- |
| `POSTGRES_DATA_DIR` | Verzeichnis auf dem Host für persistente PostgreSQL-Daten. |
| `POSTGRES_USER` | PostgreSQL-Benutzername. |
| `POSTGRES_PASSWORD` | PostgreSQL-Passwort. Wähle ein eigenes. |
| `POSTGRES_DB` | Datenbankname. |
| `POSTGRES_PORT` | Port auf dem Host für Datenbankverbindungen, normalerweise `5432`. Ändere ihn, falls dieser Port bereits belegt ist. |
| `POSTGRES_CPU_LIMIT` | CPU-Limit in Kernen, zum Beispiel `0.5` für einen halben Kern. |
| `POSTGRES_MEMORY_LIMIT` | Speicherlimit, zum Beispiel `512m` oder `1g`. |
