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
| **Archive vor Reupload zurückkopieren** | Kopiert Archive vor dem Reupload auf die lokale Festplatte. Wenn ausgeschaltet, liest der Reupload direkt aus dem Speicherordner. |

Neue Speicherordner sind aktiv. Über das Zeilenmenü kannst du sie **Aktivieren** oder **Deaktivieren**.

## Regeln für den Pfad

Bearcat legt den Speicherordner nicht an. Er muss bereits existieren und darf sich nicht mit einem
Arbeitsverzeichnis überschneiden: Er darf weder darin liegen noch eines enthalten.

Mit **Durchsuchen** wählst du einen Ordner auf dem Rechner, auf dem Bearcat läuft.
In Docker muss er über einen Bindmount erreichbar sein. Gib den Pfad innerhalb des Containers an.

## Übersicht

Die Liste zeigt den freien Speicherplatz und den eingestellten Mindestwert, die Anzahl gespeicherter Archive,
die Priorität und den Aktivierungsstatus. Ist der freie Speicherplatz nicht lesbar, steht dort **Nicht verfügbar**.

## Einen Speicherordner bearbeiten oder löschen

Solange Archive im Speicherordner liegen, kannst du seinen Pfad nicht ändern und ihn nicht löschen.
Die übrigen Einstellungen bleiben bearbeitbar.

Das Löschen entfernt nur den Eintrag aus Bearcat. Der Ordner und sein Inhalt bleiben auf der Festplatte.

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

Archive in Speicherordnern werden nie automatisch gelöscht. Reuploads lesen sie direkt von dort,
ausser **Archive vor Reupload zurückkopieren** ist aktiviert.
