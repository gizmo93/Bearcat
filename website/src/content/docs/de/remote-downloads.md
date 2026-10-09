---
title: "Releases automatisch von FTP/FTPS herunterladen"
description: "FTP/FTPS-Ordner überwachen, Releases herunterladen, prüfen und bei Bedarf entpacken."
---

Bearcat überwacht FTP- und FTPS-Server auf neue Releaseordner, lädt sie herunter und prüft vorhandene
SFV-Prüfsummen. Ein Releasetemplate legt fest, wie die Dateien danach gepackt und hochgeladen werden.

Mit einem Managed Template kannst du RAR- und 7z-Archive zuerst entpacken und danach neu packen lassen.
Das Entpacken ist standardmässig aus. Mit einem Unmanaged Template lädst du die heruntergeladenen Archive unverändert hoch.

## Server hinzufügen

![remote-sources-page.png](../images/remote-sources-page.png)

1. Öffne **Remotequellen** und klicke auf **Neue Remotequelle**.
2. Wähle **FTP / FTPS** als **Quelltyp** und gib der Verbindung einen Namen.
3. Gib **Host**, **Port**, **Benutzername** und **Passwort** ein.
4. Stelle **Verschlüsselung** passend zum Server ein: **Explizit (FTPES)**, **Implizit (FTPS)** oder **Keine (unverschlüsseltes FTP)**.
   Standard sind explizite Verschlüsselung und Port `21`.
5. Trage unter **Maximale Verbindungen** ein, wie viele Verbindungen dein Account erlaubt. Standard ist `2`.
6. Speichere und wähle dann **Verbindung testen** im Aktionsmenü des Servers.

![add-edit-remote-source1.png](../images/add-edit-remote-source1.png)
![add-edit-remote-source2.png](../images/add-edit-remote-source2.png)

Lass **TLS-Anbieter** zu Beginn auf **BouncyCastle**. Schlägt die Verbindung fehl, prüfe die
Serverdaten und versuche **System**. **Zertifikat prüfen** ist standardmässig aus. Aktiviere es, um zu
prüfen, ob der Server ein vertrauenswürdiges Zertifikat hat.

## Remoteordner überwachen

[Erstelle zuerst ein Releasetemplate](/Bearcat/de/release-templates-and-automations/#releasetemplates-einrichten).
Wähle **Managed**, um die heruntergeladenen Dateien zu packen, oder **Unmanaged**, um die vorhandenen Archive hochzuladen.

![remote-automations-page.png](../images/remote-automations-page.png)

1. Öffne **Remoteautomatisierungen** und klicke auf **Neue Remoteautomatisierung**.
2. Gib ihr einen Namen und wähle deine **Remotequelle**.
3. Setze **Remoteordner** auf den Ordner, der die Releases enthält. Mit **Durchsuchen** wählst du ihn auf dem Server aus.
4. Wähle einen lokalen **Downloadordner**. Bearcat legt dort für jedes Release einen Unterordner an.
5. Gib optional ein **Muster für Ordnernamen** ein, zum Beispiel `*1080p*`. Lass es leer, um alle Ordner einzuschliessen.
   Der Abgleich ignoriert Gross- und Kleinschreibung und nutzt Platzhalter, keine regulären Ausdrücke.
6. Wähle das **Releasetemplate** und optional eine **Primärsprache** für Metadaten.
7. Entscheide, ob du **Vorhandene Ordner ignorieren** willst, und speichere. Die Automatisierung ist standardmässig aktiviert.

![images/new-remote-automation.png](../images/new-remote-automation.png)

**Vorhandene Ordner ignorieren** ist standardmässig an. Nur Ordner, die nach dem ersten Scan hinzukommen,
werden heruntergeladen. Schalte es **vor dem Speichern** aus, um vorhandene Releases einzubeziehen.
Eine spätere Änderung wirkt sich nicht auf bereits als ignoriert markierte Ordner aus.

Bearcat muss in den Downloadordner schreiben können. Wähle in Docker einen Pfad in einem gemounteten
Volume, damit die Downloads beim Ersetzen des Containers erhalten bleiben.

### Beispiel

Mit **Remoteordner** `/movies`, **Muster für Ordnernamen** `*1080p*` und
**Downloadordner** `/data/releases/incoming`:

```text
Auf dem Server:
  /movies/Movie.One.2026.1080p/
  /movies/Movie.Two.2026.2160p/

Lokal heruntergeladen:
  /data/releases/incoming/Movie.One.2026.1080p/
```

Bearcat sucht nur in direkten Unterordnern von `/movies` nach Releases. Passende Ordner werden
mit ihrem gesamten Inhalt heruntergeladen. Eine separate lokale Ordnerautomatisierung brauchst du nicht.

### Überlappende Automatisierungen

Für andere Muster, Templates oder Downloadordner legst du weitere Automatisierungen an.
Passen mehrere Automatisierungen auf denselben Ordner einer Quelle, gilt diejenige mit der niedrigsten
**Priorität**. Zum Beispiel hat `10` Vorrang vor dem Standardwert `100`. Jeder Remoteordner wird pro Quelle nur einmal heruntergeladen.
Eine Änderung der Prioritäten ordnet bereits gefundene Ordner nicht neu zu.

### Doppelte Ordner

Bearcat markiert einen Ordner als **Duplikat**, wenn ein Release oder ein Download von einer beliebigen
Remotequelle bereits diesen Namen hat. Das Duplikat wird übersprungen, auch wenn der erste Download
fehlschlägt oder abgebrochen wird. Mit **Download neu starten** unter **Remotedownloads** lädst du es trotzdem herunter.

## Download verfolgen

Bearcat scannt etwa alle zwei Minuten. Standardmässig muss ein Ordner mindestens `1` MB gross sein, und
seine Dateianzahl und Gesamtgrösse müssen fünf Minuten lang unverändert bleiben, bevor er vorgemerkt wird.
Diese Prüfungen passt du unter [Einstellungen für Remotedownloads](/Bearcat/de/advanced-configuration/#remotedownloads) an.

Unter **Remotedownloads** siehst du Ordner, Status und Fehler. Der übliche Ablauf ist:

| Status | Bedeutung |
| --- | --- |
| Wird beobachtet | Wartet, bis sich der Ordner nicht mehr ändert und die Mindestgrösse erreicht. |
| Vorgemerkt | Bereit zum Download. |
| Wird heruntergeladen | Dateien werden lokal gespeichert. |
| Heruntergeladen | Alle Dateien sind lokal gespeichert. Als Nächstes folgen Prüfung und Entpacken. |
| Wird geprüft | Prüft die Dateien anhand der `.sfv`-Dateien im Ordner. Entfällt, wenn keine vorhanden sind. |
| Wird entpackt | Archive werden entpackt. Nur mit **Archive vor der Releaseerstellung entpacken**. |
| Bereit zur Releaseerstellung | Als Nächstes wird das Release erstellt. |
| Release erstellt | Öffne das verknüpfte Release, um seine Archive und Uploads zu verfolgen. |

![remote-downloads-page.png](../images/remote-downloads-page.png)

### SFV-Prüfung

Nach jedem Download sucht Bearcat im Releaseordner und seinen Unterordnern nach `.sfv`-Dateien und
vergleicht die CRC32-Prüfsumme jeder aufgeführten Datei. Fehlt eine aufgeführte Datei oder stimmt ihre
Prüfsumme nicht, schlägt der Download fehl, und die Dateien bleiben auf der Festplatte.

Laufende Downloads siehst du auch unter **Aktivität > Downloads** auf der Startseite, mit Fortschritt,
Geschwindigkeit, Restzeit, Phase und Dateidetails. Die Releasedetailseite zeigt die Remotequelle.

![running-remote-download.png](../images/running-remote-download.png)

### Ordner abbrechen, wiederholen oder überspringen

Nutze das Aktionsmenü unter **Remotedownloads**:

- **Abbrechen** stoppt einen beobachteten, vorgemerkten oder laufenden Download. Dateien eines laufenden Downloads werden gelöscht.
- **Download neu starten** merkt einen fehlgeschlagenen, abgebrochenen oder doppelten Download erneut vor. Er beginnt von vorn
  und entfernt Dateien des vorherigen Versuchs.
- **Ohne erneuten Download wiederholen** nutzt die vorhandenen Dateien, um Prüfung, Entpacken und
  Releaseerstellung zu wiederholen. Behebe zuerst den gemeldeten Fehler. Sind die Archive bereits
  entpackt, erstellt Bearcat nur das Release.
- **Ignorieren** überspringt einen Ordner, dessen Download noch nicht begonnen hat, oder einen fehlgeschlagenen,
  abgebrochenen oder doppelten Download. Ignorierte und doppelte Ordner blendet der Standardfilter für den Status aus,
  und sie werden nicht erneut vorgemerkt.

Ist der lokale Releaseordner vor dem Download nicht leer, bricht Bearcat mit einem Fehler ab.
Die vorhandenen Dateien bleiben erhalten. Verschiebe sie, bevor du den Download neu startest.

Eine deaktivierte Automatisierung scannt nicht mehr. Bereits vorgemerkte Downloads bleiben vorgemerkt.
Um einen davon zu stoppen, brich ihn unter **Remotedownloads** ab.

## Archive vor der Releaseerstellung entpacken

Aktiviere bei einem Managed Template **Archive vor der Releaseerstellung entpacken** in der
Automatisierung, um RAR- oder 7z-Archive zu entpacken, bevor Bearcat das Release packt.

Nach der SFV-Prüfung entpackt Bearcat alle RAR- und 7z-Archive im Releaseordner und seinen Unterordnern,
jeweils in den Ordner, in dem sie liegen. Nach erfolgreichem Entpacken löscht es die Archivparts und
`.sfv`-Dateien, die nur diese Parts aufführen, und erstellt das Release aus den entpackten Dateien.

Bearcat bricht das Entpacken ab und behält die Archive, wenn:

- nicht genug freier Speicherplatz für die entpackten Dateien vorhanden ist.
- beim Entpacken eine vorhandene Datei oder ein vorhandener Ordner überschrieben würde.
- ein Archiv beschädigt oder passwortgeschützt ist.

Diese Option ist standardmässig aus und gilt nicht für Unmanaged Templates.

## Rohdateien nach dem Upload löschen

Bei Managed Templates ist **Rohdateien behalten** standardmässig an. Schalte es in der Automatisierung aus,
wenn Bearcat den heruntergeladenen Releaseordner nach dem Upload entfernen soll.

Bearcat wartet, bis:

- jede Uploadkonfiguration einen abgeschlossenen Upload hat.
- keine Uploads warten oder laufen und keine Archive erstellt werden.
- jede Archivkonfiguration ein erstelltes Archiv oder einen [Mirror](/Bearcat/de/mirror-downloads/) hat, der online ist.

Dann wandelt es das Release in ein [Unmanaged Release](/Bearcat/de/release-types/#unmanaged-releases) um und löscht
den heruntergeladenen Ordner. Künftige Reuploads nutzen die vorhandenen Archive oder Mirrors.

Speichere Archive ausserhalb des heruntergeladenen Releaseordners. Enthält er Archive, behält Bearcat
den Ordner und benachrichtigt dich. Diese Option gilt nicht für Unmanaged Templates.
