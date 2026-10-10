---
title: "Hintergrundaufgaben und erweiterte Einstellungen"
description: "Hintergrundaufgaben, Aufräumregeln, Uploadlimits, Datenbank und Zeitzone einstellen."
---

Bearcat packt, lädt und prüft Dateien im Hintergrund. Unter **Hintergrundaufgaben** verwaltest du
die Aufgaben, unter **Konfigurationen** ihre Einstellungen.

## Hintergrundaufgaben

Jede Aufgabe läuft nach ihrem eigenen Zeitplan.

![background-tasks-page.png](../images/background-tasks-page.png)

Die Tabelle zeigt Status, Start, Ende, Dauer und den letzten Fehler jeder Aufgabe.
Mit **Aktiv** schaltest du künftige Ausführungen ein oder aus. Laufende Aufgaben werden dabei nicht abgebrochen.
Standardmässig sind alle Aufgaben aktiv.

### Verfügbare Hintergrundaufgaben

| Aufgabe | Läuft | Funktion |
| --- | ---: | --- |
| Configuration cache refresh | Alle 5 Minuten | Lädt zwischengespeicherte Einstellungen neu. Änderungen in der Oberfläche gelten sofort. |
| Release folder automation | Alle 2 Minuten | Scannt Ordner und erstellt Releases mit dem gewählten Template, sobald die [Ordnerprüfungen](#ordnerautomatisierung) bestanden sind. |
| Remote source scan | Alle 2 Minuten | Findet passende FTP-/FTPS-Ordner und plant sie ein, sobald die [Prüfungen für Remoteordner](#remotedownloads) bestanden sind. |
| Remote source download | Alle 20 Sekunden | Lädt geplante Remoteordner herunter. |
| Remote download verification and extraction | Alle 20 Sekunden | Prüft abgeschlossene Downloads anhand ihrer SFV-Dateien und entpackt Archive, falls konfiguriert. |
| Remote download release creation | Alle 20 Sekunden | Erstellt Releases aus abgeschlossenen Downloads mit den jeweils gewählten Templates. |
| Remote download raw file cleanup | Alle 2 Minuten | Wandelt heruntergeladene Releases in Unmanaged um und entfernt ihre Rohdateien, wenn [Rohdateien behalten](/Bearcat/de/remote-downloads/#rohdateien-nach-dem-upload-löschen) ausgeschaltet ist und die Bedingungen für das Löschen erfüllt sind. |
| Release info resolution | Alle 10 Minuten | Ergänzt fehlende Sceneinfos, NFOs, externe IDs und Metadaten aus den aktiven Quellen. |
| Archive creation & restore | Alle 20 Sekunden | Verwendet Archive wieder, stellt sie wieder her oder erstellt neue für wartende Uploads. |
| Auto cleanup | Alle 30 Minuten | Wendet aktivierte [Aufräumregeln](#automatisches-aufräumen) nach Ablauf ihrer Aufbewahrungsfristen an. Löscht [lokale Arbeitskopien](/Bearcat/de/archive-storage-folders/#reuploads) gespeicherter Archive, sobald ihre Uploads abgeschlossen sind. |
| Archive upload | Alle 20 Sekunden | Lädt Archive zu den konfigurierten Hostern hoch. |
| Image upload | Alle 30 Sekunden | Lädt Coverbilder von Releases zu konfigurierten Bildhostern hoch, wenn eine Cover-URL vorhanden ist. |
| Upload state check | Alle 20 Sekunden | Prüft Links und plant erste Uploads sowie automatische Reuploads. |
| Link crypter container creation | Alle 20 Sekunden | Erstellt Linkcryptercontainer für abgeschlossene Uploads. |

## Konfigurationen

Öffne "Konfigurationen" im Menü, um das globale Verhalten von Bearcat zu ändern.

Schalter und Auswahlfelder speichern sofort. Zahlenfelder speicherst oder verwirfst du über die Buttons.
Du kannst auch Enter zum Speichern oder Escape zum Abbrechen drücken.

Geänderte Einstellungen sind mit einem Punkt markiert. Der Standardwert wird daneben angezeigt.
Mit **Zurücksetzen** stellst du ihn wieder her.

### Automatisches Aufräumen

Automatisches Aufräumen ist standardmässig ausgeschaltet. Unter **Konfigurationen > Automatisches Aufräumen**
kannst du zwei unabhängige Regeln aktivieren:

- **Releases automatisch in Unmanaged umwandeln**: den Releaseordner nach einer Frist nicht mehr
  verwenden (Standard: `14` Tage). Optional aktivierst du **Releaseordner beim Umwandeln löschen**.
- **Lokale Archive nach Aufbewahrung**: **Behalten** (Standard), **Löschen** oder **In Speicherordner verschieben**
  nach der **Aufbewahrung lokaler Archive** (Standard: `30` Tage).

![auto-cleanup-config.png](../images/auto-cleanup-config.png)

#### Releases automatisch in Unmanaged umwandeln

Nach der Umwandlung lädt Bearcat vorhandene Archive erneut hoch, statt die Releasedateien neu zu packen.
Die Frist beginnt mit dem Postingdatum oder dem ersten abgeschlossenen Upload, wenn das Release nie
als gepostet markiert wurde. Ohne eines dieser Daten bleibt es managed.

Bearcat wandelt das Release nur um, wenn kein Upload läuft und jede Archivkonfiguration ein lokales
Archiv oder einen vollständig verfügbaren [Mirror](/Bearcat/de/mirror-downloads/) hat.

Standardmässig bleibt der Releaseordner erhalten. Eine Benachrichtigung enthält seinen Pfad.
Du kannst die Releasedateien selbst löschen. Liegen dort auch Archive, behalte diese Dateien.

Mit **Releaseordner beim Umwandeln löschen** löscht Bearcat auch den Ordner, aber nur wenn:

- Jede Archivkonfiguration ein vollständiges Archiv auf der Festplatte hat. Ein Mirror allein reicht nicht.
- Kein Archiv im Releaseordner liegt.
- Der Ordner weder ein Laufwerksstamm noch ein Arbeitsverzeichnis ist und kein Arbeitsverzeichnis enthält.

Sind diese Bedingungen nicht erfüllt oder schlägt das Löschen fehl, wird das Release trotzdem Unmanaged.
Der Ordner bleibt erhalten und die Benachrichtigung nennt den Grund.

#### Lokale Archive nach Aufbewahrung

Die Frist beginnt mit dem letzten abgeschlossenen Upload der Archivkonfiguration. Jeder Reupload startet sie neu.
Mit `0` Tagen kann die Aktion beim nächsten Aufräumen nach dem Upload laufen.
Bearcat überspringt Archive, solange ein Upload dieser Archivkonfiguration wartet, ansteht oder läuft.

| Aktion | Wirkung |
| --- | --- |
| **Behalten** | Archivdateien bleiben an ihrem Speicherort. |
| **Löschen** | Löscht Archivdateien nur, wenn Bearcat sie von einem vollständig verfügbaren Mirror wiederherstellen oder aus dem Ordner eines Managed Releases neu packen kann. |
| **In Speicherordner verschieben** | Verschiebt Archive in einen aktiven [Speicherordner](/Bearcat/de/archive-storage-folders/#archive-verschieben). Ein Mirror ist dafür nicht nötig. |

Beim Löschen entfernt Bearcat den Archivordner nur, wenn er leer ist. Dateien auf dem Hoster bleiben erhalten.
Archive in Speicherordnern werden nie automatisch gelöscht.

War die automatische Archivlöschung bereits aktiviert, bleibt nach dem Update die Aktion **Löschen** ausgewählt.

#### Einzelne Releases ausschliessen

Aktiviere beim Erstellen oder Bearbeiten eines Releases **Vom automatischen Aufräumen ausschliessen**,
um beide Regeln zu überspringen.

![exclude-from-auto-cleanup.png](../images/exclude-from-auto-cleanup.png)

[Remotedownloads](/Bearcat/de/remote-downloads/#rohdateien-nach-dem-upload-löschen) haben eine eigene Aufräumoption.
Lass **Rohdateien behalten** in der Automatisierung aktiviert, wenn du den heruntergeladenen Releaseordner behalten willst.

### Archivrepackaging

Hoster erkennen Dateien an ihrem MD5-Hash. Vor einem Reupload zum selben Hostertyp gibt Bearcat den
hochzuladenden Dateien neue Hashes. Nicht erneut hochgeladene Parts behalten ihre Hashes und Links.

- **RAR:** Bearcat hängt Nullbytes an, bis der Hash jeder Datei neu ist. Die Hashhistorie bleibt auch
  nach dem Löschen lokaler Dateien erhalten. Archive lassen sich weiterhin normal entpacken und die
  eingestellte Partgrösse bleibt normalerweise gleich. Kompression und Solidmodus folgen der gewählten Strategie.
- **7-Zip:** Bearcat packt das Release mit der **Repackagingstrategie** unter **Konfigurationen** neu:

| Strategie | Wirkung auf 7-Zip-Archive |
| --- | --- |
| **Archivdateigrösse um 1 MB verändern** (Standard) | Jedes neue Archiv hat `1` MB grössere Parts als das vorherige. Das erste verwendet die eingestellte Grösse. Keine Kompression, kein Solidmodus. |
| **Nur Nonce, keine Kompression** | Ändert die zufällige `__nonce.txt`. Braucht wenig CPU, aber einige Parts behalten eventuell ihren alten Hash. |
| **Solidarchive mit Kompression** | Verteilt die Nonceänderung zuverlässiger auf die Parts, braucht aber mehr CPU. |

**Noncedatei erstellen** ist in der Archivkonfiguration standardmässig aktiviert.
Ohne sie erhöht 7-Zip die Partgrösse bei jedem Neupacken um `1` MB, unabhängig von der Strategie.
RAR verwendet weiterhin Nullbytes für neue Hashes.

Bei älteren RAR-Archiven mit fehlenden Hashes bestimmt die Strategie auch die Partgrösse.
Die Standardstrategie erhöht sie beim Neupacken um `1` MB.

### Ordnerautomatisierung

Bearcat erstellt ein Release aus einem überwachten Ordner, sobald beide Bedingungen erfüllt sind:

- Anzahl und Gesamtgrösse der Dateien bleiben für die Dauer von "Ordnerstabilität" unverändert, Standard `5` Minuten.
- Der Ordner erreicht die "Minimale Ordnergrösse", Standard `1` MB.

Das verringert das Risiko, dass Bearcat noch unvollständige Ordner als Release übernimmt.

Erhöhe die Stabilitätsdauer bei langsamen Kopiervorgängen. Setze die Mindestgrösse auf `0`, um leere Ordner zuzulassen.
Manuelles Erstellen von Releases und die Metadatenabfrage unter **Releaseinfo** sind davon unabhängig.

### Remotedownloads

Die Einstellungen für [FTP-/FTPS-Downloads](/Bearcat/de/remote-downloads/) findest du unter
**Konfigurationen > Remotedownloads**. Sie gelten unabhängig von der lokalen Ordnerautomatisierung.

| Einstellung | Standard | Wirkung |
| --- | --- | --- |
| Ordnerstabilität | `5` min | Wartet, bis Dateianzahl und Gesamtgrösse des Remoteordners so lange unverändert bleiben. |
| Minimale Ordnergrösse | `1` MB | Kleinere Ordner bleiben in **Wird beobachtet**. Setze `0`, um die Grössenprüfung zu deaktivieren. |
| Maximale parallele Dateidownloads | `4` | Begrenzt, wie viele Dateien gleichzeitig heruntergeladen werden. **Maximale Verbindungen** der Quelle gilt zusätzlich. |
| Wiederholungsverzögerung | `30` s | Wartet so lange, bevor ein fehlgeschlagener Downloadversuch wiederholt wird. |

Erhöhe die Stabilitätsdauer, wenn Dateien langsam eintreffen oder Uploads auf den FTP-Server oft pausieren.
Bearcat lädt jeweils einen Releaseordner herunter, mit mehreren Dateien parallel. Senke das Verbindungslimit,
wenn der Server gleichzeitige Übertragungen verbietet.

### Cooldown für initiale Uploads

"Cooldown für initiale Uploads" ist standardmässig `5` Minuten. So hast du Zeit, ein Release fertig einzurichten, bevor Bearcat es packt und hochlädt.

Sobald eine Uploadkonfiguration existiert und das Release älter als der Cooldown ist, plant der nächste Lauf von "Upload state check" den initialen Upload ein.
Die Wartezeit beginnt mit der Erstellung des Releases. Setze sie auf `0`, damit der erste Upload direkt bei der nächsten Prüfung geplant wird.

Änderungen betreffen nur Uploadkonfigurationen ohne Upload. Bestehende Uploads werden weder abgebrochen noch verzögert.

### Parallele Uploads

"Maximale parallele Uploads" ist standardmässig `10`. Der Wert limitiert parallele Dateiuploads über alle Hoster hinweg.
Das Limit jedes Hosters gilt zusätzlich.

Auf der Seite "Hosterregistrierungen" siehst du die einzelnen Limits und kannst sie, wo möglich, überschreiben. Siehe [Parallele Uploads pro Hoster](/Bearcat/de/account-settings/#parallele-uploads-pro-hoster).

"Maximale Uploadgeschwindigkeit" (MB/s) ist standardmässig leer, also ohne Limit. Der Wert limitiert die gesamte
Uploadgeschwindigkeit aller laufenden Uploads über alle Hoster hinweg. Kommazahlen wie `0.5` oder `0,5` sind erlaubt.
Jede Hosterregistrierung kann zusätzlich ein eigenes Limit setzen. Siehe
[Uploadgeschwindigkeit pro Hoster begrenzen](/Bearcat/de/account-settings/#uploadgeschwindigkeit-pro-hoster-begrenzen).

Bei sehr niedrigen Geschwindigkeitslimits können Uploads wegen Zeitüberschreitungen scheitern.

## Datenbank

Neue Installationen als Desktopanwendung, Windows-Dienst oder Docker verwenden SQLite. Bestehende PostgreSQL-Installationen verwenden weiterhin PostgreSQL.

Die Datenbankeinstellungen werden beim Start aus Umgebungsvariablen oder einer Einstellungsdatei gelesen:

| Installation | Wo du sie setzt |
| --- | --- |
| Docker | `docker-compose.yml`, oder `docker-compose.postgres.yml` für PostgreSQL. Siehe [Bearcat in Docker ausführen](/Bearcat/de/use-the-docker-image/#datenbank). |
| Desktopanwendung | **Database type** in den Einstellungen der [Desktopanwendung](/Bearcat/de/use-the-desktop-launcher/#datenbanktyp). |
| Windows-Dienst | Abschnitt `Database` in `%ProgramData%\Bearcat\config.json`, geschrieben von `Bearcat.Cli.exe setup`. |

| Einstellung | Umgebungsvariable | Wert |
| --- | --- | --- |
| `Database:Provider` | `Database__Provider` | `Sqlite` oder `Postgres`, ohne Beachtung der Gross- und Kleinschreibung. Nicht gesetzt: `Postgres`. |
| `Database:SqliteFilePath` | `Database__SqliteFilePath` | Pfad zur SQLite-Datenbankdatei. Nicht gesetzt: `bearcat.db` im Datenverzeichnis. |
| `Database:ConnectionString` | `Database__ConnectionString` | PostgreSQL-Verbindungsdaten, zum Beispiel `Host=localhost;Database=bearcat;Username=bearcat;Password=...`. Wird nur mit `Postgres` verwendet. |

Bearcat bestimmt das Datenverzeichnis in dieser Reihenfolge:

1. Umgebungsvariable `BEARCAT_DATA_DIR`
2. Einstellung `Bearcat:DataDirectory` (Umgebungsvariable `Bearcat__DataDirectory`)
3. `/data` innerhalb eines Containers
4. Der Anwendungsdatenordner des Benutzers plus `Bearcat`: `%APPDATA%\Bearcat` unter Windows, `~/Library/Application Support/Bearcat` unter macOS

Der Windows-Dienst verwendet `%ProgramData%\Bearcat` als Datenverzeichnis.

Hinweise zu SQLite:

- Lege die SQLite-Datenbank auf ein lokales Laufwerk. Netzwerkfreigaben (NFS/SMB) eignen sich nicht für den verwendeten WAL-Modus.
- Sichere die Datenbank zusammen mit `bearcat.key` aus dem Datenverzeichnis. Stoppe Bearcat vor dem Kopieren.
  Für eine Sicherung bei laufendem Bearcat verwende `sqlite3 bearcat.db ".backup bearcat-backup.db"`.

Eine Änderung von `Database:Provider` überträgt keine vorhandenen Daten. Eine neue Datenbank startet leer.
Ohne `Database:Provider` verwendet Bearcat weiterhin PostgreSQL, damit ältere Konfigurationen funktionieren.

## Zeitzone

Bearcat verwendet die lokale Zeitzone für angezeigte Zeiten und zeitabhängige Regeln.
Standardmässig gilt die Zeitzone des Betriebssystems.

| Installation | Standard | Ändern |
| --- | --- | --- |
| Desktopanwendung | Zeitzone deines Computers | Ändere die Zeitzone deines Computers. |
| Windows-Dienst | Zeitzone des Windows-Servers | Füge `"LocalTimezone": "Europe/Berlin"` zu `%ProgramData%\Bearcat\config.json` hinzu und starte den Dienst neu. |
| Docker | UTC | Setze `BEARCAT_TIMEZONE` in `.env`. Siehe [Dockereinstellungen](/Bearcat/de/use-the-docker-image/#docker-einstellungen). |

Der Wert ist eine Zeitzonen-ID wie `Europe/Berlin` oder `America/New_York`.
Bei einer unbekannten ID bricht Bearcat den Start ab.
