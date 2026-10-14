---
title: "Accountpools"
description: "Uploads anhand des freien Speicherplatzes auf mehrere Accounts eines Hosters verteilen."
---

Ein Accountpool fasst mehrere Accounts desselben Hosters zusammen. Eine Uploadkonfiguration oder ein
Releasetemplate kann statt eines einzelnen Accounts einen Pool als **Uploadziel** verwenden. Bearcat
wählt den Account, wenn der Upload startet.

## Unterstützte Hoster

Pools funktionieren nur mit Hostern, die den freien Speicherplatz eines Accounts melden:

- Rapidgator
- Alfafile
- Fast2Share.com
- XFileSharing-basierte Hoster: clicknupload.click, datavaults.co, ddownload, file-upload.org,
  fileq.net, fileserve.com, freedl.ink, hxfile.co, katfile.com, mega4upload.net, uploady.io

## Pool anlegen

Öffne **Hoster** > **Accountpools** und klicke auf **Neuer Pool**. Wähle den Hoster, füge die Accounts
hinzu und ziehe sie in die gewünschte Reihenfolge. Der erste Account wird zuerst verwendet. Der Hoster
lässt sich später nicht ändern.

**Mindestspeicherplatz** ist der Speicherplatz, der nach einem Upload auf jedem Account frei bleiben muss.

## Accountauswahl

Wenn ein Poolupload startet, prüft Bearcat die Accounts in der Reihenfolge des Pools und weist den
ersten passenden zu:

- Inaktive Accounts, Accounts mit unlesbaren Zugangsdaten und Accounts, die auf eine
  CAPTCHA-Verifizierung warten, werden übersprungen.
- Ein Account passt, wenn sein freier Speicherplatz abzüglich der Dateien, die gerade noch auf ihn
  hochgeladen werden, und abzüglich des Mindestspeicherplatzes mindestens so gross ist wie der Upload.
- Accounts mit unbegrenztem Speicherplatz passen immer.
- Lehnt der Hoster die Zugangsdaten ab, deaktiviert Bearcat den Account und versucht den nächsten.
- Lässt sich der freie Speicherplatz nicht abfragen, überspringt Bearcat den Account für 5 Minuten.

Bearcat speichert den freien Speicherplatz jedes Accounts 5 Minuten lang zwischen.

## Pool voll

Passt kein Account, bleibt der Upload ausstehend und Bearcat erstellt einmal pro Upload eine
Benachrichtigung **Accountpool voll**. Bearcat prüft den Pool bei jedem Uploaddurchlauf erneut und
startet den Upload, sobald ein Account genug Platz hat, etwa nachdem du einen Account hinzugefügt oder
Speicherplatz freigegeben hast.

## Reuploads

Ein Reupload bleibt auf dem Account des vorherigen Uploads, wenn dieser Account noch aktiv und Teil des
Pools ist. Den freien Speicherplatz prüft Bearcat in diesem Fall nicht. Sonst bekommt der Reupload beim
Start einen Account aus dem Pool.

## Parallele Uploads

Das Limit für parallele Uploads gilt pro Hoster, nicht pro Account. Alle Accounts eines Pools teilen es
sich. Siehe [Parallele Uploads pro Hoster](/Bearcat/de/account-settings/#parallele-uploads-pro-hoster).
