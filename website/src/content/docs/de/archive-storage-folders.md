---
title: "Archivspeicherordner"
description: "Archive nach der Aufbewahrungsfrist auf einem NAS, einer weiteren Festplatte oder einer Netzwerkfreigabe speichern."
---

Mit Speicherordnern lagerst du Archive aus deinen Arbeitsverzeichnissen aus, etwa auf ein NAS oder
eine zweite Festplatte. Bearcat prüft, ob der Ordner erreichbar ist und genug Platz hat, aber nicht,
ob dort das erwartete Laufwerk eingebunden ist.

## Einen Speicherordner hinzufügen

Öffne **Konfiguration > Releaseeinstellungen > Archivspeicherordner** und klicke auf **Neuer Speicherordner**.

| Feld | Bedeutung |
| --- | --- |
| **Name** | Eindeutiger Name für den Speicherordner. |
| **Pfad** | Absoluter Pfad eines vorhandenen Ordners. Jeder Pfad kann nur einmal registriert werden. |
| **Freizuhaltender Speicherplatz (GB)** | So viel Platz muss nach dem Verschieben noch frei sein. Mit `0` gilt kein Mindestwert. |
| **Priorität** | Niedrigere Werte haben Vorrang. |
| **Lokale Arbeitskopie für Reuploads** | Kopiert die Dateien, die ein Reupload braucht, in eine temporäre lokale Arbeitskopie. Wenn ausgeschaltet, liest der Reupload direkt aus dem Speicherordner. |

Neue Speicherordner sind aktiv. Über das Zeilenmenü kannst du sie **Aktivieren** oder **Deaktivieren**.

## Regeln für den Pfad

Bearcat legt den Speicherordner nicht an. Er muss bereits existieren und darf sich nicht mit einem
Arbeitsverzeichnis überschneiden: Er darf weder darin liegen noch eines enthalten.

Mit **Durchsuchen** wählst du einen Ordner auf dem Rechner, auf dem Bearcat läuft.
In Docker muss er über einen Bindmount erreichbar sein. Gib den Pfad innerhalb des Containers an.

## Übersicht

Die Liste zeigt den freien Speicherplatz und den eingestellten Mindestwert, die Anzahl gespeicherter Archive,
die Priorität und den Aktivierungsstatus. Ist der freie Speicherplatz nicht lesbar, steht dort **Nicht verfügbar**.
Als gespeichert zählen nur aktuelle Archive. Gelöschte, durch einen Neuimport ersetzte und Archive mit
fehlenden Dateien zählen nicht.

## Einen Speicherordner bearbeiten oder löschen

Solange Archive im Speicherordner liegen, kannst du seinen Pfad nicht ändern und ihn nicht löschen.
Die übrigen Einstellungen bleiben bearbeitbar.

Das Löschen entfernt nur den Eintrag aus Bearcat. Der Ordner und sein Inhalt bleiben auf der Festplatte.
Archive, die nicht mehr als gespeichert zählen, verlieren ihren Verweis auf den gelöschten Speicherordner.

## Archive verschieben

Stelle unter [Automatisches Aufräumen](/Bearcat/de/advanced-configuration/#lokale-archive-nach-aufbewahrung)
**Lokale Archive nach Aufbewahrung** auf **In Speicherordner verschieben**.
Bearcat verschiebt die Archive dann nach Ablauf der Aufbewahrungsfrist.

Bearcat wählt für jedes Archiv einen aktiven, erreichbaren Speicherordner mit genug Platz für alle
Archivdateien und den freizuhaltenden Speicherplatz. Die niedrigste Priorität hat Vorrang.
Bei gleicher Priorität gewinnt der Ordner mit dem meisten freien Platz.
Ein Archiv bleibt immer zusammen in einem Unterordner.

Bearcat kopiert die Dateien und prüft ihre Grösse. Erst danach verwendet es den neuen Speicherort
und löscht die ursprünglichen Archivdateien. Den ursprünglichen Archivordner entfernt es nur, wenn er leer ist.

Unter **Aktivität** siehst du Fortschritt und Ziel. Mit **Verschieben abbrechen** entfernst du die
bisher kopierten Dateien; das Archiv bleibt am ursprünglichen Ort.

Fehlt ein passender Speicherordner oder schlägt das Kopieren fehl, bleibt das Archiv am ursprünglichen Ort.
Bearcat benachrichtigt dich und versucht es im nächsten Durchlauf erneut.
Weitere Meldungen zum selben Fehler folgen erst, wenn du die Benachrichtigung erledigt hast.

Archive in Speicherordnern werden nie automatisch gelöscht.
Auch **Lokale Archive löschen** auf der Releasedetailseite lässt sie aus.

## Reuploads

Braucht ein Reupload ein Archiv aus einem Speicherordner, entscheidet **Lokale Arbeitskopie für Reuploads**,
wie Bearcat es verwendet.

**Aus:** Der Upload liest die Archivdateien direkt aus dem Speicherordner. Auch Hashänderungen für den
Reupload schreibt Bearcat dort, der Speicherordner muss also beschreibbar sein.

**Ein:** Bearcat kopiert nur die Archivdateien, die hochgeladen werden, in eine temporäre lokale Arbeitskopie.
Die Arbeitskopie ist ein Unterordner mit dem Ordnernamen des Archivs im **Basispfad der Archivdateien**
der Archivkonfiguration. Enthält dieser Unterordner bereits andere Dateien, verwendet Bearcat stattdessen
`<Ordnername>.<Archiv-ID>`. Dateien, die von einem früheren Reupload noch in einer Arbeitskopie liegen,
verwendet Bearcat wieder.

- Der Upload wartet, bis das Kopieren abgeschlossen ist.
- Hashänderungen und der Upload verwenden die lokalen Dateien.
- Ein Reupload schreibt nie in den Speicherordner. Das Archiv bleibt seinem Speicherordner zugeordnet.
- Sind die Uploads der Archivkonfiguration abgeschlossen, stellt die Aufgabe
  [Auto cleanup](/Bearcat/de/advanced-configuration/#verfügbare-hintergrundaufgaben) das Archiv wieder auf
  den Speicherordner um und löscht die Arbeitskopie. Das passiert unabhängig von den Aufräumregeln.
  Fehlgeschlagene Uploads verzögern es nicht.

Unter **Aktivität** zeigt **Kopieren in lokale Arbeitskopien** den Fortschritt.
Mit **Kopieren abbrechen** entfernst du die bisher kopierten Dateien und der Upload liest das Archiv aus dem
Speicherordner.

Schlägt das Kopieren fehl, entfernt Bearcat die kopierten Dateien und der Upload liest das Archiv aus dem
Speicherordner. Das gilt auch, wenn der **Basispfad der Archivdateien** nicht existiert: Bearcat legt ihn nie
an, nur den Unterordner darin. Bearcat benachrichtigt dich pro Archiv einmal, bis du die Benachrichtigung
erledigt hast.

Ist der Speicherordner nicht erreichbar oder fehlen dort Archivdateien, behandelt Bearcat das Archiv wie
jedes Archiv mit fehlenden Dateien: Es stellt die Dateien von einem [Mirror](/Bearcat/de/mirror-downloads/)
wieder her oder packt Managed Releases neu. Bearcat legt den Speicherordner nie an.

## Wo gespeicherte Archive angezeigt werden

Der Tab **Archive** der Releasedetailseite und die Liste **Archive** zeigen den Namen des Speicherordners
neben Archiven, die dort liegen. Bei Unmanaged Releases steht er zusätzlich neben dem Archivordner im Kopfbereich.
Solange ein Archiv eine lokale Arbeitskopie hat, folgt dem Namen **lokale Arbeitskopie**.

Änderst du den Archivordner eines Unmanaged Release, gehört das Archiv zu dem Speicherordner, der den
neuen Ordner enthält, oder zu keinem.
