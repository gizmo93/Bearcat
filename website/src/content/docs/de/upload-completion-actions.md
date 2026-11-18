---
title: "Aktionen nach Upload"
description: "Aktionen wie einen IntelliGeN-XML-Export automatisch ausführen, sobald ein Release vollständig hochgeladen ist."
---

Aktionen nach Upload laufen automatisch, sobald ein Release vollständig hochgeladen ist.
Die erste verfügbare Aktion ist der **IntelliGeN XML export**.

## Eine Aktion hinzufügen

1. Öffne **Automatisierung > Aktionen nach Upload** in der Seitenleiste und klicke auf **Neue Aktion**.
2. Gib einen Namen ein und wähle den **Aktionstyp**.
3. Lege fest, wann die Aktion läuft:
   - **Beim ersten Upload ausführen** läuft, sobald alle Uploads eines Release zum ersten Mal abgeschlossen sind.
   - **Beim Reupload ausführen** läuft erneut, sobald ein Reupload eines Release abgeschlossen ist.
4. Fülle die Konfiguration aus und klicke auf **Speichern**.

Neue Aktionen sind aktiv. Mit **Deaktivieren** im Aktionsmenü pausierst du eine Aktion.

## Verhalten

- Bearcat prüft alle 5 Minuten, ob Uploads, Linkcontainer und Bilder eines Release fertig sind.
- Jede Aktion läuft einmal pro abgeschlossenem Uploadstand. Ein späterer Reupload zählt als neuer Stand.
- Aktionen laufen nur für Uploads, die nach dem Anlegen der Aktion abgeschlossen wurden. Ältere
  Releases werden nicht verarbeitet.
- Eine fehlgeschlagene Aktion wird bei der nächsten Prüfung automatisch wiederholt. Bearcat erstellt
  eine Benachrichtigung für den ersten Fehler, nicht für jede Wiederholung.

Der Abschnitt **Aktionen nach Upload** im Übersichtstab der
[Releasedetailseite](/Bearcat/de/release-detail-page/) zeigt das letzte Ergebnis jeder aktiven Aktion.
**Jetzt ausführen** führt eine Aktion sofort aus, auch wenn sie schon gelaufen ist oder ihr Auslöser
ausgeschaltet ist. Der Button ist verfügbar, sobald alle Uploads des Release abgeschlossen sind.
Ein fehlgeschlagener manueller Lauf erstellt keine Benachrichtigung.

## IntelliGeN-XML-Export

Der Export schreibt pro Lauf eine XML-Datei, die IntelliGeN importieren kann.

| Feld | Bedeutung |
| --- | --- |
| **Exportordner** | Ordner, in den die XML-Dateien geschrieben werden. Er muss bereits existieren. Bearcat legt ihn nie an. |
| **Dateipfadvorlage** | Pfad der Datei relativ zum Exportordner. Er darf Unterordner enthalten. |

Vorhandene Dateien werden nie überschrieben. Bearcat hängt stattdessen eine Nummer an, zum Beispiel
`Release.Name (2).xml`.

Die Dateipfadvorlage verwendet die Syntax von [Scriban](https://github.com/scriban/scriban).
Standard ist `{{ release.name }}.xml`. Verfügbare Variablen:

| Variable | Wert |
| --- | --- |
| `{{ release.name }}` | Releasename |
| `{{ release.title }}` | Titel aus den Metadaten |
| `{{ release.content_type }}` | Inhaltstyp |
| `{{ release.platform }}` | Plattform eines Spiels |
| `{{ release.genre }}` | Genre |
| `{{ release.runtime_minutes }}` | Laufzeit in Minuten |
| `{{ release.video_codec }}` | Videocodec des Hauptvideos |
| `{{ release.audio_codec }}` | Audiocodec der Haupttonspur |
| `{{ is_reupload }}` | `true` bei einem Reupload, sonst `false` |

Beispiel, das Reuploads in einen Unterordner legt:

```text
{{ if is_reupload }}Reuploads/{{ end }}{{ release.name }}.xml
```

### In IntelliGeN importieren

Richte **DirWatch** von IntelliGeN auf den Exportordner. IntelliGeN übernimmt dann jede neue Datei.

Crypter und Dropdownwerte wie Genre, Sprache oder Codecs übernimmt IntelliGeN nur, wenn der Wert in
seinen eigenen Listen existiert. Andere Werte ignoriert IntelliGeN.
