---
title: "Hoster als Langzeitspeicher nutzen"
description: "Lokale Archive löschen und bei Bedarf für Reuploads von einem Mirrorhoster herunterladen."
---

Aktiviere einen Hoster als Mirror, damit Bearcat Archivdateien wiederherstellen kann, wenn ein Reupload sie braucht.
Danach kannst du lokale Archive löschen, um Speicherplatz freizugeben, solange ein Mirror die benötigten Dateien noch online hat.

Das funktioniert für [Managed und Unmanaged Releases](/Bearcat/de/release-types/).

![running-mirror-download.png](../images/running-mirror-download.png)

## Wann Bearcat von einem Mirror herunterlädt

Braucht ein Upload ein vorhandenes Archiv, verwendet Bearcat zuerst die Dateien auf der Festplatte.
Fehlende Dateien lädt es von aktiven Mirrorhostern herunter. Die benötigten Parts dürfen dabei auf
mehrere Mirrors verteilt sein.

Bearcat übernimmt Links, die vom vorherigen Upload zum selben Hoster noch online sind.
Es lädt nur die Dateien herunter, die für den Reupload fehlen.

Ist kein passender Mirror verfügbar, kann Bearcat [Managed Releases](/Bearcat/de/release-types/) aus
dem Releaseordner neu packen. Bei Unmanaged Releases musst du die fehlenden Archivdateien selbst bereitstellen.
Siehe [Unmanaged Releases](/Bearcat/de/release-types/#unmanaged-releases).

7-Zip-Archive müssen für neue Hashes neu gepackt werden, auch wenn die Dateien vorhanden sind.
Die Unterschiede stehen unter [Archivwiederverwendung](/Bearcat/de/upload-lifecycle/#archivwiederverwendung-und-repackaging).

## Hoster als Mirror aktivieren

Öffne die Hosterregistrierung und schalte **Für Mirrordownloads verwenden** ein.

![enable-hoster-mirror-download.png](../images/enable-hoster-mirror-download.png)

### Proxy für Downloads verwenden

Setze **Proxy für Mirrordownloads** in der Hosterregistrierung, um für Archivwiederherstellungen einen Proxy
zu verwenden. Er ist unabhängig vom Proxy für Uploads und Accountanfragen. Lass ihn auf **Kategoriestandard**,
um der globalen Einstellung **Mirrordownloads von Hostern** zu folgen. Siehe [Proxyserver](/Bearcat/de/proxy-servers/).

### Mirrorpriorität

**Mirrorpriorität** erscheint, wenn du Mirrordownloads aktivierst. Standard: `100`.
Niedrigere Zahlen haben Vorrang, etwa `10` vor `100`.

Bearcat wählt den Mirror für jede Datei einzeln. Es berücksichtigt nur aktive Hosterregistrierungen,
bei denen die Datei als online markiert ist. Bei gleicher Priorität gewinnt die jüngste Dateiprüfung.
Schlägt ein Download trotz Wiederholungen fehl, versucht Bearcat den nächsten verfügbaren Mirror.

### Hoster mit Unterstützung für Mirrordownloads

**Für Mirrordownloads verwenden** ist bei diesen Hostern verfügbar:

| Hoster | Premiumaccount nötig |
| --- | --- |
| Rapidgator | Ja |
| 1fichier | Ja |
| Alfafile | Ja |
| HxFile.co | Ja |
| Keep2Share | Ja |
| Fast2Share.com | Ja |
| mega4upload.net | Ja |
| DDownload | Nein |
| datavaults.co | Nein |
| file-upload.org | Nein |
| Uploady.io | Nein |

Bearcat zeigt neben dem Schalter einen Hinweis, wenn ein Premiumaccount nötig ist. Mit kostenlosen Accounts
kannst du trotzdem zu diesen Hostern hochladen.

Für **mega4upload.net** braucht dein API-Schlüssel zusätzlich den Scope `download`.
Bitte den Support, ihn freizuschalten.

## Laufenden Download abbrechen

Verfolge oder beende Mirrordownloads unter **Aktivität > Downloads**. Dort siehst du Fortschritt,
Geschwindigkeit, Restzeit und Dateidetails.

Beim Abbrechen löscht Bearcat die unvollständigen Downloads und bricht die wartenden Uploads ab.
Sie starten beim nächsten Durchlauf nicht erneut. Der Archivstatus wird zurückgesetzt; die Dateien beim Hoster bleiben erhalten. Erstelle einen manuellen Reupload, wenn du es erneut versuchen willst.

## Prüfung und Fehler

Bearcat vergleicht heruntergeladene Dateien mit dem beim Upload gespeicherten MD5-Hash, sofern er vorhanden ist.
Bei einem abweichenden Hash oder Downloadfehler versucht es die Datei erneut oder nutzt einen anderen Mirror.

Kann Bearcat eine Datei auch danach nicht wiederherstellen, verwirft es den Download und meldet
**Archivwiederherstellung fehlgeschlagen**. Das Archiv behält seinen bisherigen Status; die wartenden Uploads schlagen fehl.
Behebe die Ursache und erstelle danach einen manuellen Reupload.

## Parallele Downloads

Unter **Konfigurationen > Mirrordownloads** legst du mit **Maximale parallele Downloads** fest, wie viele
Archivdateien gleichzeitig heruntergeladen werden. Standard: `2`.

Erhöhe den Wert, wenn deine Leitung schnell ist und dein Hosteraccount mehrere Verbindungen erlaubt. Senke
ihn auf `1`, wenn der Hoster parallele Downloads drosselt oder ablehnt.

## Geschwindigkeitslimits

- **Maximale Downloadgeschwindigkeit** (MB/s) im Abschnitt **Mirrordownloads** auf der Seite **Konfigurationen**
  begrenzt die gemeinsame Geschwindigkeit aller laufenden Mirrordownloads.
- **Maximale Downloadgeschwindigkeit für Mirrordownloads (MB/s)** in der Hosterregistrierung begrenzt die
  gemeinsame Geschwindigkeit aller Mirrordownloads dieser Hosterregistrierung.

Leere Felder bedeuten kein Limit und sind der Standard. Kommazahlen wie `0.5` oder `0,5` sind erlaubt.
Beide Limits gelten gleichzeitig.

## Lokale Archive selbst löschen

Wähle **Lokale Archive löschen** im Aktionsmenü der Releasedetailseite, um Speicherplatz freizugeben.
Bearcat löscht die Archivdateien des Releases.

![delete-local-archives.png](../images/delete-local-archives.png)

Bearcat bietet das nur an, wenn das Release wiederherstellbar bleibt:

- Kein Upload des Releases läuft.
- Jede Archivkonfiguration hat einen vollständig verfügbaren Upload auf einem Hoster, der für
  Mirrordownloads aktiviert ist, **oder** das Release ist managed und hat noch seinen Releaseordner zum Neupacken.

Bearcat löscht die Archivdateien und entfernt den Ordner nur, wenn er danach leer ist. Andere Dateien bleiben erhalten.

## Automatisches Aufräumen

Unter [Automatisches Aufräumen](/Bearcat/de/advanced-configuration/#lokale-archive-nach-aufbewahrung)
kannst du **Lokale Archive nach Aufbewahrung** auf **Löschen** stellen.
Bearcat entfernt sie nach der eingestellten Frist und stellt sie bei Bedarf für einen Reupload wieder her.
