---
title: "Hoster ersetzen"
description: "Uploadkonfigurationen und Vorlagen von einem Hoster auf einen anderen Account oder Pool umstellen."
---

Öffne **Hoster** > **Hoster ersetzen**. Wähle den Hoster, die zu ersetzenden Accounts und Pools sowie einen
Ersatzaccount oder Ersatzpool derselben Art (Video oder Archiv). Die Vorschau aktualisiert sich bei jeder
Änderung. Klicke auf **Hoster ersetzen**, um die Auswahl zu übernehmen. Bearcat kann das nicht automatisch
rückgängig machen.

## Was ersetzt wird

- Uploadkonfigurationen in Releases und Releasetemplates verwenden danach den gewählten Account oder Pool.
- Eine Konfiguration, die wie der alte Account oder Pool heisst, übernimmt den Namen des Ersatzes. Andere
  Namen bleiben unverändert.
- **Premium only** wird ausgeschaltet, wenn der Ersatzhoster das nicht unterstützt.
- Uploads, die noch nicht gestartet sind und keine Dateien haben, wechseln zum Ersatz.

Die Pools selbst bleiben unverändert. Unbenutzte Pools kannst du unter
[Accountpools](/Bearcat/de/account-pools/) löschen.

Bestehende Uploads bleiben beim alten Hoster. Erst der nächste Reupload verwendet den gewählten Account
oder Pool.

## Übersprungene Konfigurationen

| Fall | Ergebnis |
|---|---|
| Das Release oder Template hat dieselbe Archivkonfiguration bereits beim Ersatzhoster | Die Konfiguration bleibt beim alten Hoster |
| Mehrere ausgewählte Accounts verwenden dieselbe Archivkonfiguration eines Releases oder Templates | Nur die erste Uploadkonfiguration wird umgestellt |
| Eine Konfiguration eines Uploadslots wird übersprungen | Der gesamte Uploadslot bleibt unverändert |

Die Vorschau listet übersprungene Konfigurationen mit einem Link zum Release oder Template auf.

## Doppelte Konfigurationen löschen

Aktiviere **Doppelte Konfigurationen löschen**, um doppelte Uploadkonfigurationen zu löschen. Als doppelt
gelten Konfigurationen, deren Archivkonfiguration im selben Release oder Template bereits beim
Ersatzhoster vorhanden ist. Verwenden mehrere ausgewählte Accounts dieselbe Archivkonfiguration, stellt
Bearcat die erste Uploadkonfiguration um und löscht die übrigen.

- Bearcat löscht ihre Uploads und Links.
- Container bei Linkcryptern bleiben bestehen. Lösche sie bei Bedarf beim Linkcrypter.
- Ein Uploadslot wird als Ganzes gelöscht, wenn alle seine Konfigurationen doppelt sind und keinen laufenden
  Upload haben. Andernfalls bleibt der gesamte Slot unverändert.
- Konfigurationen mit einem laufenden Upload werden nicht gelöscht.
- Doppelte Konfigurationen in Templates werden immer gelöscht.

Die Vorschau zeigt, welche Konfigurationen und Uploadslots gelöscht werden. Was Bearcat nicht löschen kann,
steht weiterhin unter den übersprungenen Konfigurationen. Das Löschen lässt sich nicht rückgängig machen.

## Hoster ist tot

Ein toter Hoster meldet Dateien meist nicht mehr als offline. Inaktive Accounts prüft Bearcat ebenfalls
nicht. Ohne diese Option gelten bestehende Uploads in Bearcat weiterhin als online und es startet kein
Reupload.

Mit **Hoster ist tot** führt Bearcat zwei weitere Schritte aus:

- Bearcat deaktiviert die ausgewählten Accounts und alle Accounts der ausgewählten Pools. Das gilt auch für
  Accounts, die du nicht einzeln ausgewählt hast oder die zusätzlich in anderen Pools sind.
- Bearcat markiert abgeschlossene Uploads dieser Accounts und deren Dateien als offline, auch Uploads von
  übersprungenen Konfigurationen.

Die Option steht nicht zur Verfügung, wenn der Ersatz beim selben Hoster liegt.

[Automatische Reuploads](/Bearcat/de/upload-lifecycle/#7-automatische-reuploads) starten nach der üblichen
Wartezeit beim neuen Account oder Pool. Dafür müssen **Automatische Reuploads** in der Releasegruppe aktiv
sein. Übersprungene Konfigurationen verwenden weiterhin den deaktivierten Account. Für sie startet erst
ein Reupload, wenn du sie anpasst.

## Was sich nicht ändert

- Laufende Uploads und Uploads, die schon Dateien haben, bleiben beim alten Hoster. Brich sie ab, falls sie
  nicht fertig werden.
- Links in bestehenden Forenposts ändern sich nicht.
- Fest eingetragene Hosternamen in Forenpostvorlagen musst du von Hand ändern. `upload.hoster_name` zeigt
  den Hosternamen des letzten Uploads.
