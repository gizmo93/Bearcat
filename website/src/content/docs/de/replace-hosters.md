---
title: "Hoster ersetzen"
description: "Uploadkonfigurationen und Vorlagen von einem Hoster auf einen anderen Account oder Pool umstellen."
---

Öffne **Hoster** > **Hoster ersetzen**. Wähle den Hoster, die zu ersetzenden Accounts und Pools sowie einen
Ersatzaccount oder Ersatzpool derselben Art (Video oder Archiv). Die Vorschau aktualisiert sich bei jeder
Änderung. Klicke auf **Hoster ersetzen**, um die Änderung anzuwenden. Bearcat kann das nicht automatisch
rückgängig machen.

## Was ersetzt wird

- Uploadkonfigurationen von Releases und in Releasetemplates bekommen den Ersatz als Uploadziel.
- Eine Konfiguration, die wie der alte Account oder Pool heisst, übernimmt den Namen des Ersatzes. Andere
  Namen bleiben.
- **Premium only** wird ausgeschaltet, wenn der Ersatzhoster das nicht unterstützt.
- Uploads, die noch nicht gestartet sind und keine Dateien haben, wechseln zum Ersatz.

Pools werden nie verändert. Nur das Uploadziel der Konfiguration ändert sich. Ein unbenutzter Pool bleibt
bestehen und du kannst ihn unter [Accountpools](/Bearcat/de/account-pools/) löschen.

Bestehende Uploads bleiben bis zum nächsten Reupload beim alten Hoster. Der nächste Reupload verwendet den
Ersatz.

## Übersprungene Konfigurationen

| Fall | Ergebnis |
|---|---|
| Das Release oder Template hat dieselbe Archivkonfiguration bereits beim Ersatzhoster | Die Konfiguration bleibt beim alten Hoster |
| Mehrere ausgewählte Accounts verwenden dieselbe Archivkonfiguration eines Releases oder Templates | Die erste wird ersetzt, die anderen bleiben |
| Eine Konfiguration eines Uploadslots wird übersprungen | Alle Konfigurationen dieses Uploadslots bleiben |

Die Vorschau listet übersprungene Konfigurationen mit einem Link zum Release oder Template auf.

## Doppelte Konfigurationen löschen

Mit **Doppelte Konfigurationen löschen** werden Konfigurationen, die übersprungen werden, weil das Release
oder Template dieselbe Archivkonfiguration bereits beim Ersatzhoster hat, gelöscht statt behalten. Das gilt
auch für die weiteren Konfigurationen, wenn mehrere ausgewählte Accounts dieselbe Archivkonfiguration
verwenden.

- Bearcat löscht ihre Uploads und Links.
- Container bei Linkcryptern bleiben bestehen. Lösche sie bei Bedarf beim Linkcrypter.
- Konfigurationen eines Uploadslots werden nicht gelöscht.
- Konfigurationen mit einem laufenden Upload werden nicht gelöscht.
- Für Konfigurationen in Templates gibt es keine Ausnahmen.

Nicht gelöschte Konfigurationen bleiben in der Liste der übersprungenen Konfigurationen. Die Vorschau listet
die zu löschenden Konfigurationen auf. Das Löschen lässt sich nicht rückgängig machen.

## Hoster ist tot

Ein toter Hoster meldet Dateien meist nie als offline, und Bearcat prüft inaktive Accounts nicht. Ohne diese
Option bleiben bestehende Uploads in Bearcat online und es startet kein Reupload.

Mit **Hoster ist tot** macht Bearcat zusätzlich Folgendes:

- Deaktiviert die ausgewählten Accounts und alle Mitglieder der ausgewählten Pools, auch Mitglieder, die du
  nicht ausgewählt hast oder die in anderen Pools sind.
- Markiert abgeschlossene Uploads dieser Accounts und deren Dateien als offline, auch Uploads von
  übersprungenen Konfigurationen.

Die Option steht nicht zur Verfügung, wenn der Ersatz beim selben Hoster liegt.

[Automatische Reuploads](/Bearcat/de/upload-lifecycle/#7-automatische-reuploads) laufen danach nach der
üblichen Wartezeit beim Ersatz. Dafür müssen **Automatische Reuploads** in der Releasegruppe aktiv sein.
Übersprungene Konfigurationen bleiben beim deaktivierten Account und bekommen keinen Reupload, bis du sie
änderst.

## Was sich nicht ändert

- Laufende Uploads und Uploads, die schon Dateien haben, bleiben beim alten Hoster. Brich sie ab, falls sie
  nicht fertig werden.
- Links in bestehenden Forenposts ändern sich nicht.
- Hosternamen, die wörtlich in Forenpostvorlagen stehen, musst du von Hand ändern. `upload.hoster_name` folgt
  dem letzten Upload.
