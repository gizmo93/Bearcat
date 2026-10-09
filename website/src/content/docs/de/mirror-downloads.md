---
title: "Hoster als Langzeitspeicher nutzen"
description: "Lokale Archive löschen und bei Bedarf für Reuploads von einem Mirrorhoster herunterladen."
---

Aktiviere einen Hoster als Mirror, damit Bearcat Archivdateien wiederherstellen kann, wenn ein Reupload sie braucht.
Danach kannst du lokale Archive löschen, um Speicherplatz freizugeben, solange ein Mirror die benötigten Dateien noch online hat.

Das funktioniert für [Managed und Unmanaged Releases](/Bearcat/de/release-types/).

![running-mirror-download.png](../images/running-mirror-download.png)

## Wann Bearcat von einem Mirror herunterlädt

Bearcat lädt ein Archiv von einem Mirror herunter, wenn:

- ein Upload auf sein Archiv wartet,
- das neueste Archiv der Archivkonfiguration lokal fehlt oder unvollständig ist und
- alle benötigten Dateien auf einem Mirrorhoster online sind.

Wenn die Archivwiederverwendung möglich ist, sucht Bearcat Dateien in dieser Reihenfolge:

1. Die lokalen Archivdateien, falls sie noch auf der Festplatte liegen.
2. Download von einem Mirrorhoster.
3. Nur bei Managed Releases: neu packen aus dem Releaseordner.

Für neue Hashes muss Bearcat 7-Zip-Archive auch bei vorhandenen Dateien neu packen.
Die Unterschiede zwischen den Formaten stehen unter [Archivwiederverwendung](/Bearcat/de/upload-lifecycle/#archivwiederverwendung-und-repackaging).

Fehlen bei einem Unmanaged Release lokale Archive und Mirrors, wartet der Upload, bis du die
Archivdateien bereitstellst. Die Anleitung findest du unter
[Unmanaged Releases](/Bearcat/de/release-types/#unmanaged-releases).

Bearcat übernimmt Dateien, die von einem früheren Upload zum selben Hoster noch online sind,
und lädt nur die fehlenden herunter.

## Hoster als Mirror aktivieren

Öffne die Hosterregistrierung und schalte **Für Mirrordownloads verwenden** ein.

![enable-hoster-mirror-download.png](../images/enable-hoster-mirror-download.png)

### Proxy für Downloads verwenden

Setze **Proxy für Mirrordownloads** in der Hosterregistrierung, um für Archivwiederherstellungen einen Proxy
zu verwenden. Er ist unabhängig vom Proxy für Uploads und Accountanfragen. Lass ihn auf **Kategoriestandard**,
um der globalen Einstellung **Mirrordownloads von Hostern** zu folgen. Siehe [Proxyserver](/Bearcat/de/proxy-servers/).

### Mirrorpriorität

**Mirrorpriorität** erscheint, wenn du Mirrordownloads aktivierst. Der Standard ist `100`; niedrigere
Zahlen haben Vorrang. Ein Mirror mit `10` wird also vor einem mit `100` gewählt.

Bearcat berücksichtigt nur aktive Mirrorregistrierungen, bei denen alle benötigten Dateien als online markiert sind.
Haben mehrere Mirrors dieselbe Priorität, wählt es den mit der jüngsten Dateiprüfung.

Die Hosterliste zeigt die Priorität neben **Mirrordownloads**, zum Beispiel **Mirrordownloads (Prio: 100)**.

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

<details>
<summary>Warum manche Hoster einen Premiumaccount brauchen</summary>

- **Alfafile:** kostenlose Accounts erlauben einen Download alle 120 Minuten und insgesamt nur 500 MB Traffic.
- **Fast2Share:** kostenlose Accounts haben für jede Datei eine Wartezeit. Bearcat unterstützt das nicht,
  daher schlagen Mirrordownloads mit einem kostenlosen Account fehl.
- **Keep2Share:** kostenlose Downloads sind auf etwa 50 KB/s und eine Verbindung gleichzeitig begrenzt.
- **mega4upload.net:** Downloads brauchen einen Premiumaccount und einen API-Schlüssel mit dem
  Scope `download`. Bitte den Support von mega4upload, den Scope zu deinem Schlüssel hinzuzufügen.

</details>

## Laufenden Download abbrechen

Verfolge oder beende Mirrordownloads unter **Aktivität > Downloads**. Dort siehst du Fortschritt,
Geschwindigkeit, Restzeit und Dateidetails.

Beim Abbrechen löscht Bearcat die unvollständigen Downloads und bricht die wartenden Uploads ab.
Sie starten beim nächsten Durchlauf nicht erneut. Der Archivstatus wird zurückgesetzt; die Dateien beim Hoster bleiben erhalten. Erstelle einen manuellen Reupload, wenn du es erneut versuchen willst.

## Prüfung und Fehler

Bearcat prüft jede heruntergeladene Datei gegen den beim Upload gespeicherten MD5-Hash. Stimmt
ein Hash nicht oder schlägt ein Download fehl, verwirft es die Wiederherstellung, belässt das Archiv
im bisherigen Zustand und erstellt die Benachrichtigung **Archivwiederherstellung fehlgeschlagen**.

Auch die wartenden Uploads schlagen fehl. Behebe die Ursache und erstelle danach einen manuellen Reupload.

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
Sind beide gesetzt, gilt das niedrigere Limit.

## Lokale Archive selbst löschen

Wähle **Lokale Archive löschen** im Aktionsmenü der Releasedetailseite, um Speicherplatz freizugeben.
Die Aktion löscht die lokalen Archivdateien des Releases und markiert die Archive als gelöscht.

![delete-local-archives.png](../images/delete-local-archives.png)

Bearcat bietet das nur an, wenn das Release wiederherstellbar bleibt:

- Kein Upload des Releases läuft.
- Jede Archivkonfiguration hat einen vollständig verfügbaren Upload auf einem Hoster, der für
  Mirrordownloads aktiviert ist, **oder** das Release ist managed und hat noch seinen Releaseordner zum Neupacken.

Bearcat löscht die Archivdateien und entfernt den Ordner nur, wenn er danach leer ist. Andere Dateien bleiben erhalten.

## Automatisches Aufräumen

Das automatische Aufräumen kann lokale Archive nach einer konfigurierten Aufbewahrungsdauer löschen. Bearcat
lädt sie von einem Mirror herunter, wenn ein späterer Upload sie braucht. Die zwei Regeln und ihre Einstellungen
findest du unter [Automatisches Aufräumen](/Bearcat/de/advanced-configuration/#automatisches-aufräumen).
