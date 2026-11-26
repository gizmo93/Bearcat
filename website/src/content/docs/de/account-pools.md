---
title: "Accountpools"
description: "Uploads auf mehrere Accounts eines Hosters mit genug freiem Speicherplatz verteilen."
---

Ein Accountpool fasst mehrere Accounts desselben Hosters zusammen. Wähle in einer Uploadkonfiguration
oder einem Releasetemplate den Pool als **Uploadziel**. Bearcat wählt beim Uploadstart einen Account
mit genug freiem Speicherplatz.

## Unterstützte Hoster

Pools funktionieren nur mit Hostern, die den freien Speicherplatz eines Accounts melden:

- Rapidgator
- Alfafile
- Fast2Share.com
- Zippyshare.day
- XFileSharing-basierte Hoster: clicknupload.click, datavaults.co, ddownload, file-upload.org,
  fileq.net, fileserve.com, freedl.ink, hxfile.co, katfile.com, mega4upload.net, uploady.io

## Pool anlegen

Öffne **Registrierungen > Hoster** > **Accountpools** und klicke auf **Neuer Pool**. Wähle den Hoster, füge die Accounts
hinzu und ziehe sie in die gewünschte Reihenfolge. Bearcat prüft die Accounts von oben nach unten.
Den Hoster kannst du später nicht mehr ändern.

Mit **Mindestspeicherplatz** legst du fest, wie viel Speicherplatz nach einem Upload auf jedem Account
frei bleiben muss.

## Accountauswahl

Beim Uploadstart geht Bearcat die Accounts der Reihe nach durch und wählt den ersten mit genug Platz.
Dabei gelten diese Regeln:

- Inaktive Accounts sowie Accounts mit unlesbaren Zugangsdaten oder einem ausstehenden Captcha werden
  übersprungen.
- Vom freien Speicherplatz zieht Bearcat den Mindestspeicherplatz und die noch offenen Dateien laufender
  Uploads ab. Der Rest muss für den neuen Upload reichen.
- Accounts mit unbegrenztem Speicherplatz haben immer genug Platz.
- Lehnt der Hoster die Zugangsdaten ab, deaktiviert Bearcat den Account und probiert den nächsten.
- Lässt sich der freie Speicherplatz nicht abfragen, wird der Account für 5 Minuten übersprungen.

Die Ergebnisse der Speicherplatzabfrage werden 5 Minuten lang zwischengespeichert.

## Pool voll

Hat kein Account genug Platz, wartet der Upload. Bearcat meldet einmal pro Upload **Accountpool voll**.
Ist der Pool leer oder ist keiner seiner Accounts nutzbar, meldet es stattdessen
**Accountpool ohne nutzbaren Account**. Prüfe dann, ob die Accounts aktiv sind, gültige Zugangsdaten
haben und kein Captcha aussteht.

Bearcat prüft den Pool bei jedem Uploaddurchlauf erneut. Sobald ein Account genug Platz hat, startet
der Upload automatisch. Du kannst dafür einen Account hinzufügen oder Speicherplatz freigeben.

## Reuploads

Ein Reupload verwendet den bisherigen Account weiter, solange er noch im Pool und aktiv ist, lesbare
Zugangsdaten hat und kein Captcha aussteht. Der freie Speicherplatz wird dabei nicht geprüft. Andernfalls
wählt Bearcat beim Start einen anderen Account aus dem Pool. Das gilt auch, wenn der Account unbrauchbar
wird, bevor die erste Datei hochgeladen ist.

## Account entfernen

Du kannst einen Account nicht löschen, solange dort noch die neuesten Uploads einer Uploadkonfiguration
liegen, die einen Pool oder einen anderen Account als Ziel verwendet. Bearcat würde diese Releases sonst
erneut hochladen. Entferne den Account stattdessen aus dem Pool und deaktiviere ihn. Er bekommt keine
neuen Uploads mehr, seine bestehenden Uploads bleiben aber erhalten.

## Pool ersetzen

Mit [Hoster ersetzen](/de/replace-hosters/) stellst du alle Uploadkonfigurationen eines Pools auf
einen anderen Account oder Pool um.

## Posts

In Posts ist `upload.hoster_name` der Anzeigename des Hosters (z. B. "Rapidgator"), nicht der Name
des Pools oder Accounts. `upload.hoster_registration_name` zeigt den Accountnamen des letzten Uploads.

## Parallele Uploads

Das Limit für parallele Uploads gilt gemeinsam für alle Accounts desselben Hosters.
Siehe [Parallele Uploads pro Hoster](/de/account-settings/#parallele-uploads-pro-hoster).
