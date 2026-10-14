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
- XFileSharing-basierte Hoster: clicknupload.click, datavaults.co, ddownload, file-upload.org,
  fileq.net, fileserve.com, freedl.ink, hxfile.co, katfile.com, mega4upload.net, uploady.io

## Pool anlegen

Öffne **Hoster** > **Accountpools** und klicke auf **Neuer Pool**. Wähle den Hoster, füge die Accounts
hinzu und ziehe sie in die gewünschte Reihenfolge. Bearcat prüft die Accounts von oben nach unten.
Den Hoster kannst du später nicht mehr ändern.

Mit **Mindestspeicherplatz** legst du fest, wie viel Speicherplatz nach einem Upload auf jedem Account
frei bleiben muss.

## Accountauswahl

Beim Uploadstart geht Bearcat die Accounts der Reihe nach durch und wählt den ersten mit genug Platz.
Dabei gelten diese Regeln:

- Bearcat überspringt inaktive Accounts und Accounts mit unlesbaren Zugangsdaten oder einem
  ausstehenden Captcha.
- Vom freien Speicherplatz zieht Bearcat den Platz für die übrigen Dateien laufender Uploads und den
  Mindestspeicherplatz ab. Der Rest muss für den neuen Upload reichen.
- Accounts mit unbegrenztem Speicherplatz haben immer genug Platz.
- Lehnt der Hoster die Zugangsdaten ab, deaktiviert Bearcat den Account und versucht den nächsten.
- Lässt sich der freie Speicherplatz nicht abfragen, überspringt Bearcat den Account für 5 Minuten.

Bearcat speichert das Ergebnis der Speicherplatzabfrage für 5 Minuten.

## Pool voll

Hat kein Account genug Platz, wartet der Upload. Bearcat meldet einmal pro Upload **Accountpool voll**.

Bearcat prüft den Pool bei jedem Uploaddurchlauf erneut. Sobald ein Account genug Platz hat, startet
der Upload automatisch. Du kannst dafür einen Account hinzufügen oder Speicherplatz freigeben.

## Reuploads

Ein Reupload verwendet den bisherigen Account, solange er aktiv und im Pool ist. Bearcat prüft dabei
den freien Speicherplatz nicht. Andernfalls wählt Bearcat beim Start einen anderen Account aus dem Pool.

## Parallele Uploads

Das Limit für parallele Uploads gilt gemeinsam für alle Accounts desselben Hosters.
Siehe [Parallele Uploads pro Hoster](/Bearcat/de/account-settings/#parallele-uploads-pro-hoster).
