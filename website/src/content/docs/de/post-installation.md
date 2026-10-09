---
title: "Dein erster Upload"
description: "Füge ein Hosterkonto hinzu, wähle einen Ordner und lade dein erstes Release hoch."
---

Starte Bearcat und halte dein Hosterkonto und deine Dateien bereit. Die Dateien, die du gemeinsam hochlädst, bilden ein **Release**.

Öffne Bearcat im Browser:

- **Desktopanwendung oder Windows-Dienst:** [http://127.0.0.1:17208](http://127.0.0.1:17208)
- **Docker:** [http://localhost:8080](http://localhost:8080) oder `http://<server-address>:8080` auf einem anderen Rechner

Falls du den Webport geändert hast, verwende diesen Port.

## 1. Hosterkonto hinzufügen

1. Öffne **Hosterregistrierungen** und klicke auf **Neuer Hoster**.
2. Wähle deinen Hoster und gib Benutzername und Passwort oder API-Schlüssel ein.
3. Speichere und klicke dann auf **Login testen**, um die Verbindung zu prüfen.

Lass Uploadlimits und Reuploadeinstellungen auf den Standardwerten.

<details>
<summary>Hostereinrichtung anzeigen</summary>

![Hosterkonto registrieren](../images/register-hoster.png)
![Hosterlogin testen](../images/try-login.png)

</details>

## 2. Releasegruppe erstellen

Öffne **Releasegruppen**, klicke auf **Neue Releasegruppe** und nenne sie `My uploads`.
Lass automatische Reuploads für diesen ersten Upload deaktiviert und speichere.

Die Gruppe legt fest, ob Bearcat Uploads ersetzt, die offline gehen. Bearcat prüft die Links auch bei deaktivierten Reuploads.

## 3. Dateien wählen

Lege die Dateien für deinen ersten Upload in einen Unterordner deines Arbeitsverzeichnisses, zum Beispiel
`releases/My.First.Upload/`.

1. Öffne **Releases** und klicke auf **Neues Release**.
2. Wähle **Managed**. Damit erstellt Bearcat die Archive für dich.
3. Wähle den Ordner `My.First.Upload`. Lass den Namen leer, um den Ordnernamen zu verwenden.
4. Wähle die Gruppe `My uploads` und klicke auf **Release erstellen**.
5. Klicke auf den Releasenamen, um die Detailseite zu öffnen.

In Docker erscheint dein Ordner `RELEASES_DIR` in der Ordnerauswahl als `/mnt/data/releases`.
Hast du bereits fertige Archive zum Hochladen, verwende ein [Unmanaged Release](/Bearcat/de/release-types/#unmanaged-releases).

## 4. Archiverstellung festlegen

Klicke im Tab **Archive** auf **Hinzufügen**:

- Nenne die Konfiguration `Main archive` und wähle RAR oder 7z.
- Wähle einen beschreibbaren Ausgabeordner in einem Arbeitsverzeichnis, getrennt von den Quelldateien.
- Setze das Dateipräfix auf `My.First.Upload`. Bearcat ergänzt die Dateiendung.
- Wähle eine Partgrösse in MB, die das Dateigrössenlimit deines Hosters einhält.
- Setze bei Bedarf ein Passwort und speichere.

## 5. Uploadziel festlegen

Klicke unter **Uploads** > **Konfiguration** auf **Hinzufügen**. Gib der Konfiguration einen Namen,
wähle dein Hosterkonto und `Main archive` und speichere. Lass **Links verteilt an** vorerst leer.

## 6. Auf den Upload warten und Links kopieren

Standardmässig packt Bearcat die Dateien und lädt sie automatisch hoch, sobald das Release fünf Minuten alt ist.
Diese Wartezeit kannst du in den **Konfigurationen** ändern.

Öffne **Uploads** und wechsle zu **Verlauf**, um den Fortschritt zu verfolgen. Ist der Upload fertig, kopiere die Downloadlinks aus der **Übersicht**.

![Uploads eines Releases](../images/release-uploads-tab.png)

Schlägt der Upload fehl oder startet er nicht, prüfe die Fehler unter der Benachrichtigungsglocke.
Stelle sicher, dass **Login testen** erfolgreich ist, beide Konfigurationen gespeichert sind und
der Ausgabeordner der Archive beschreibbar ist. Die Phasen beschreibt [Ablauf eines Uploads](/Bearcat/de/upload-lifecycle/).

## Nach deinem ersten Upload

Für weitere Uploads kannst du:

- [Template speichern und Ordner überwachen](/Bearcat/de/release-templates-and-automations/), um diese Einstellungen wiederzuverwenden.
- [Reuploads aktivieren oder Linkcrypter hinzufügen](/Bearcat/de/account-settings/), um deine Uploadlinks zu verwalten.
- [Von FTP oder FTPS herunterladen](/Bearcat/de/remote-downloads/), um Releases automatisch abzurufen.
- [Metadaten und Coverbilder hinzufügen](/Bearcat/de/release-information-and-metadata/) für deine Releases.
- [Die Releasedetailseite ansehen](/Bearcat/de/release-detail-page/), um weitere Einstellungen zu finden.
