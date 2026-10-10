---
title: "Archivspeicherordner"
description: "Ordner ausserhalb der Arbeitsverzeichnisse registrieren, die Archive aufnehmen können."
---

Ein Speicherordner ist ein Ordner ausserhalb deiner Arbeitsverzeichnisse, der Archive aufnehmen kann,
zum Beispiel auf einem NAS, einer zweiten Festplatte oder einer Netzwerkfreigabe. Bearcat behandelt ihn
als normales Verzeichnis und prüft nicht, was dahinter liegt.

## Einen Speicherordner hinzufügen

Öffne in der Navigation **Konfiguration > Releaseeinstellungen > Archivspeicherordner**.
Du findest die Seite auch mit **Ctrl+K** oder **Cmd+K**.

Klicke auf **Neuer Speicherordner** und fülle die Felder aus:

| Feld | Bedeutung |
| --- | --- |
| **Name** | Eindeutiger Name, höchstens 100 Zeichen. |
| **Pfad** | Absoluter Pfad eines vorhandenen Ordners, höchstens 500 Zeichen. Muss eindeutig sein. |
| **Freizuhaltender Speicherplatz (GB)** | Speicherplatz, der im Ordner nach dem Verschieben von Archiven frei bleiben muss. `0` oder mehr. |
| **Priorität** | Niedrigere Werte werden bevorzugt. Mehrere Ordner können dieselbe Priorität haben. |
| **Archive vor Reupload zurückkopieren** | An: Archive werden vor einem Reupload auf die lokale Festplatte zurückkopiert. Aus: Ein Reupload liest sie direkt aus dem Speicherordner. |

Neue Speicherordner sind aktiv. Mit **Aktivieren** oder **Deaktivieren** im Zeilenmenü änderst du das.

## Regeln für den Pfad

- Der Pfad muss absolut sein und der Ordner muss bereits existieren. Bearcat legt nie einen Speicherordner an.
- Der Pfad darf kein Arbeitsverzeichnis sein, nicht in einem liegen und keines enthalten.
- Mit **Durchsuchen** wählst du einen beliebigen Ordner auf dem Rechner, auf dem Bearcat läuft.
- In Docker gibst du den Pfad innerhalb des Containers an. Der Ordner muss dort über einen Bind Mount
  erreichbar sein.

## Übersicht

Die Liste zeigt für jeden Speicherordner:

- Name und Pfad
- Priorität
- aktuell freien Speicherplatz des Pfads, oder **Nicht verfügbar**, wenn der Ordner nicht gefunden wird
- den eingestellten freizuhaltenden Speicherplatz
- die Anzahl der dort gespeicherten Archive
- ob Archive vor einem Reupload zurückkopiert werden
- ob der Ordner aktiv ist

## Einen Speicherordner bearbeiten oder löschen

Der Pfad kann nicht geändert werden, solange Archive im Ordner gespeichert sind. Alle anderen Felder
kannst du jederzeit ändern.

Ein Speicherordner, der noch Archive enthält, kann nicht gelöscht werden. Beim Löschen entfernt Bearcat
den Speicherordner nur aus seiner Konfiguration. Der Ordner und sein Inhalt auf der Festplatte bleiben erhalten.
