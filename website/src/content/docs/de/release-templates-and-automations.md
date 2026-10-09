---
title: "Releasetemplates und Ordnerautomatisierungen"
description: "Uploadeinstellungen wiederverwenden und Releases automatisch aus neuen Ordnern erstellen."
---

Speichere die Einstellungen deines [ersten Uploads](/Bearcat/de/post-installation/) als Template.
Damit erstellst du weitere Releases von Hand oder automatisch aus neuen Ordnern.

## Releasetemplates einrichten

Ein Releasetemplate enthält die Releasegruppe sowie die Einstellungen für Archive, Uploads, Bilder und Linkcrypter.

- Öffne **Releasetemplates**, klicke auf **Neues Releasetemplate** und füge die Konfigurationen hinzu.
- Öffne ein konfiguriertes Release und wähle im Aktionsmenü **Als Template speichern**.

![Releasetemplates](../images/release-templates-page.png)

Wähle auf **Releases** den Eintrag **Neu aus Template**. Das neue Release übernimmt normalerweise
den Ordnernamen, ebenso die Archivkonfigurationen, die den Releasenamen verwenden.

Aktiviere **Collectionerkennung** im Template, um zusammengehörige Releases zu gruppieren, etwa die Folgen
einer TV-Staffel. Siehe [Releasecollections](/Bearcat/de/release-collections/).

## Ordnerautomatisierungen einrichten

1. Erstelle ein Releasetemplate, öffne dann **Ordnerautomatisierungen** und klicke auf **Neue Ordnerautomatisierung**.
2. Setze **Releasebasispfad** auf den Ordner, den Bearcat durchsuchen soll. Nur direkte Unterordner werden geprüft.
3. Setze optional ein **Muster für Ordnernamen**, etwa `*1080p*` oder `*.GERMAN.*`. Der Abgleich ignoriert
   Gross- und Kleinschreibung und unterstützt Wildcards, aber keine regulären Ausdrücke. Lass das Feld leer,
   um alle direkten Unterordner einzubeziehen.
4. Wähle das Template und optional eine Primärsprache für übersetzte Metadaten. Ohne Sprache wird die
   Standardsprache des Metadatenanbieters verwendet.
5. Lass **Aktiviert** eingeschaltet und speichere. Über das Aktionsmenü kannst du die Automatisierung deaktivieren
   oder wieder aktivieren.

![Ordnerautomatisierungen](../images/folder-automations-page.png)

Für diesen Basisordner:

```text
/data/releases/incoming/
  Movie.One.2026.1080p/
  Movie.Two.2026.2160p/
  Some.Other.Folder/
```

Das Muster `*1080p*` wählt nur `Movie.One.2026.1080p` aus. Ein leeres Muster schliesst alle drei Ordner ein.

Bearcat scannt etwa alle zwei Minuten und überspringt Ordner, die schon ein Release haben. Sobald
ein Ordner die [Vorgaben für Stabilität und Mindestgrösse](/Bearcat/de/advanced-configuration/#ordnerautomatisierung)
erfüllt, erstellt Bearcat das Release und ruft die Metadaten ab. Danach packt es die Dateien und
lädt sie mit den Einstellungen des Templates hoch.

### Archive vor der Releaseerstellung entpacken

Aktiviere bei Managed Templates in der Ordnerautomatisierung **Archive vor der Releaseerstellung entpacken**,
wenn die Ordner RAR- oder 7z-Archive enthalten und Bearcat das Release selbst packen soll. Die Option ist
standardmässig deaktiviert und gilt nicht für Unmanaged Templates.

Bevor das Release erstellt wird, prüft Bearcat den Ordner anhand seiner `.sfv`-Dateien, falls vorhanden,
und entpackt alle RAR- und 7z-Archive im Ordner und in seinen Unterordnern. Nach erfolgreichem Entpacken
löscht es die Archivparts und die `.sfv`-Dateien, die nur diese Parts auflisten. Es gelten dieselben
Regeln wie bei [Remotedownloads](/Bearcat/de/remote-downloads/#archive-vor-der-releaseerstellung-entpacken).

Laufende Prüfungen und Entpackvorgänge erscheinen auf der Seite **Aktivität** unter **Ordnerautomatisierungen**.

Schlägt die Prüfung oder das Entpacken fehl, behält Bearcat die Dateien und erstellt kein Release.
Du findest den Fehler unter **Fehlgeschlagene Entpackvorgänge** auf **Ordnerautomatisierungen**.
Behebe die Ursache und klicke auf **Erneut versuchen**, damit Bearcat den Ordner beim nächsten Scan
verarbeitet. Bearcat versucht es auch erneut, wenn sich die Dateien im Ordner ändern.

### Releases von FTP oder FTPS herunterladen

Mit **Remotequellen** und **Remoteautomatisierungen** lädst du neue Ordner von einem Server herunter und
erstellst daraus Releases mit einem Template. Einrichtung und Beispiele findest du unter
[FTP/FTPS-Downloads](/Bearcat/de/remote-downloads/).
