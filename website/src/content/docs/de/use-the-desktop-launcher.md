---
title: "Bearcat unter Windows und macOS installieren"
description: "Installiere die Desktopanwendung, wähle deinen Releaseordner und starte Bearcat mit SQLite."
prev:
  label: "Installation wählen"
  link: "/Bearcat/de/#erste-schritte"
next:
  label: "Dein erster Upload"
  link: "/Bearcat/de/post-installation/"
---

Die Desktopanwendung startet Bearcat und öffnet die Weboberfläche im Browser.
Neue Installationen verwenden **SQLite** und brauchen keinen Datenbankserver.

## 1. Archivprogramme installieren

Bearcat braucht zum Starten sowohl RAR als auch 7-Zip.

| System | Was du installierst |
| --- | --- |
| Windows | [WinRAR](https://www.rarlab.com/download.htm) und [7-Zip](https://www.7-zip.org/download.html). |
| macOS | [RAR for macOS ARM](https://www.rarlab.com/download.htm) und die [7-Zip-Konsolenversion für macOS](https://www.7-zip.org/download.html). Entpacke beide in Ordner, die du behältst. |

Wähle in Schritt 3 die Programmdateien aus. Unter Windows sind das meist
`C:\Program Files\WinRAR\Rar.exe` und `C:\Program Files\7-Zip\7z.exe`.
Unter macOS wählst du die entpackten Dateien `rar` und `7zz`.

Unter macOS kannst du [7-Zip auch über Homebrew](https://formulae.brew.sh/formula/sevenzip) installieren:

```bash
brew install sevenzip
```

Wähle bei Homebrew in Schritt 3 unter **7z executable** den Pfad `/opt/homebrew/bin/7zz`
(der Standardpfad auf Apple Silicon).

[Der Homebrew-Cask für RAR](https://formulae.brew.sh/cask/rar) (`brew install --cask rar`) ist deaktiviert.
Verwende den offiziellen Download oben. Ist RAR bereits über Homebrew installiert,
wähle unter **RAR executable** den Pfad `/opt/homebrew/bin/rar`.

## 2. Bearcat herunterladen und öffnen

Lade das Desktoppaket für deinen Rechner von [GitHub Releases](https://github.com/gizmo93/Bearcat/releases) herunter:

- **Windows:** Entpacke das Paket `Bearcat.Desktop-win-...zip` und öffne `Bearcat.Desktop.exe`. Lass alle entpackten Dateien im selben Ordner.
- **macOS (Apple Silicon):** Entpacke `Bearcat.Desktop-macos-arm64.zip`, verschiebe **Bearcat Desktop.app** in den Ordner **Programme** und öffne die App.

<details>
<summary>macOS meldet, die App sei beschädigt, oder blockiert das Öffnen</summary>

Falls macOS die App blockiert, verschiebe sie nach `/Applications` und führe im Terminal aus:

```bash
xattr -dr com.apple.quarantine "/Applications/Bearcat Desktop.app"
```

Öffne die App erneut.

</details>

## 3. Ordner und Programme wählen

Erstelle einen Ordner für deine Releases, zum Beispiel `C:\Bearcat\releases` unter Windows oder
`~/Bearcat/releases` unter macOS. Fülle in der Desktopanwendung diese Felder aus:

| Feld | Eingabe |
| --- | --- |
| **Working directories** | Klicke auf **Add...** und wähle deinen Releaseordner. Bearcat muss hier lesen und schreiben können. |
| **RAR executable** | Klicke auf **Browse...** und wähle unter Windows `Rar.exe` oder unter macOS `rar`. |
| **7z executable** | Klicke auf **Browse...** und wähle unter Windows `7z.exe` oder unter macOS `7zz`. |

Behalte die Standardwerte für **SQLite (recommended)**, **Database file**, **Bearcat Host**, **Web port** und **API key**.

## 4. Bearcat starten

Klicke auf **Save** und dann auf **Start Bearcat**. Sobald Bearcat bereit ist, klicke auf **Open Bearcat** oder öffne
[http://127.0.0.1:17208](http://127.0.0.1:17208).

**Weiter: [Hosterkonto hinzufügen und erstes Release hochladen](/Bearcat/de/post-installation/).**

Schliesst du das Einstellungsfenster, läuft Bearcat weiter. Mit **Stop** oder **Quit** beendest du es.
Soll Bearcat unter Windows auch ohne Anmeldung laufen, verwende den [Windows-Dienst](/Bearcat/de/use-the-windows-service/).

## Weitere Einstellungen

### Datenbanktyp

SQLite speichert deine Daten in einer Datei auf diesem Rechner. Lege sie auf ein lokales Laufwerk, nicht auf eine Netzwerkfreigabe.
Der Standardspeicherort ist:

```text
Windows: %APPDATA%\Bearcat\bearcat.db
macOS: ~/Library/Application Support/Bearcat/bearcat.db
```

Für PostgreSQL [richtest du einen PostgreSQL-Server ein](/Bearcat/de/install-postgresql-for-desktop/), wählst
**PostgreSQL** und gibst Host, Port, Datenbankname, Benutzername und Passwort ein.
Bearcat aktualisiert die Datenbank beim Start und legt sie an, wenn der Benutzer die Berechtigung dazu hat.

Bestehende PostgreSQL-Einstellungen bleiben erhalten. Ein Datenbankwechsel überträgt keine Daten; die neue Datenbank startet leer.

### Bearcat Host

Lass das Feld für die automatische Erkennung leer. Schlägt die Erkennung fehl, wähle die Datei `Bearcat.Host`
oder `Bearcat.Host.dll` aus dem heruntergeladenen Paket.

### Web port

Behalte `17208`, ausser eine andere Anwendung belegt diesen Port. Änderst du ihn, verwende den neuen Port in der Browseradresse.

### API key

Lass das Feld leer, ausser du willst [Bearcat über die REST-API steuern](/Bearcat/de/external-orchestration/#api-schlüssel).

## Einstellungen und Backups

Die Desktopanwendung speichert ihre Einstellungen hier:

```text
Windows: %APPDATA%\Bearcat\Desktop\settings.json
macOS: ~/Library/Application Support/Bearcat/Desktop/settings.json
```

Die Datei enthält auch einen API-Schlüssel und ein PostgreSQL-Passwort, falls du diese eingegeben hast.

Bearcat erstellt ausserdem `bearcat.key` in `%APPDATA%\Bearcat` unter Windows bzw.
`~/Library/Application Support/Bearcat` unter macOS. Damit entschlüsselt Bearcat deine gespeicherten Zugangsdaten.

Bewahre die Datenbank und `bearcat.key` zusammen auf. Ohne den Schlüssel kann Bearcat nach einer
Wiederherstellung oder einem Rechnerwechsel deine Zugangsdaten nicht entschlüsseln.

- **SQLite:** Stoppe Bearcat und kopiere `bearcat.db` vom konfigurierten Speicherort. Läuft Bearcat, verwende `sqlite3 bearcat.db ".backup bearcat-backup.db"`, statt die Datenbankdatei zu kopieren.
- **PostgreSQL:** Sichere die PostgreSQL-Datenbank.

Kopiere bei beiden Varianten auch `bearcat.key`.

## Wenn Bearcat nicht startet

- **RAR oder 7z wurde nicht gefunden:** Wähle die Programmdateien über **Browse...** aus. Unter macOS heisst die Programmdatei des offiziellen 7-Zip-Downloads `7zz`.
- **Ein Ordner fehlt oder ist nicht beschreibbar:** Prüfe deine Arbeitsverzeichnisse und den Speicherort der Datenbankdatei.
- **Der Webport ist belegt:** Wähle einen anderen Port und starte Bearcat erneut.
- **Änderungen scheinen unter Windows keine Wirkung zu haben:** Prüfe im Infobereich der Taskleiste, ob eine weitere Instanz von Bearcat Desktop läuft, und beende sie.
