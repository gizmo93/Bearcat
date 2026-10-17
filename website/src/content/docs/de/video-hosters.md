---
title: "Videohoster"
description: "Die Hauptvideodatei eines Releases zu einem Videohoster hochladen und einen Dateilink und einen Einbettungslink erhalten."
---

Ein Videohoster nimmt eine einzelne Videodatei entgegen und liefert einen Dateilink und einen
Einbettungslink. Über den Einbettungslink bindest du einen Player für das Video ein.

Mit der Uploadquelle **Videodatei** lädt eine Uploadkonfiguration die Hauptvideodatei des Releases direkt
zu einem Videohoster hoch. Bearcat erstellt dabei keine Archivparts und lädt keine hoch.

## Unterstützte Videohoster

- MixDrop

## Voraussetzungen

- Nur [Managed Releases](/Bearcat/de/release-types/) mit dem Inhaltstyp **Film** oder **Serienfolge**.
- Bei Releasetemplates: nur Managed Templates mit dem Inhaltstyp **Film** oder **Serienfolge**.

## Hauptvideo

Bearcat lädt nur das Hauptvideo hoch: die grösste Mediendatei mit einem Videostream, ermittelt aus den
extrahierten Mediendaten. Wurden keine Mediendaten extrahiert oder hat keine Datei einen Videostream,
schlägt der Upload mit einem Fehler fehl.

## Videouploadkonfiguration anlegen

1. Öffne die Releasedetailseite und füge eine Uploadkonfiguration hinzu.
2. Wähle unter **Uploadquelle** die Option **Videodatei**. Das Feld erscheint nur, wenn das Release
   Videouploads erlaubt.
3. Wähle als **Uploadziel** einen Account oder einen [Accountpool](/Bearcat/de/account-pools/) eines Videohosters.

Eine Videouploadkonfiguration hat keine Archivkonfiguration. Bei **Videodatei** zeigt die Liste der
Uploadziele nur Accounts und Pools von Videohostern. Bei **Archivdateien** zeigt sie nur die übrigen Hoster.
Die Uploadquelle kannst du nach dem Anlegen nicht mehr ändern.

## Releasetemplates

Füge in einem [Releasetemplate](/Bearcat/de/release-templates-and-automations/) ein
Uploadkonfigurationstemplate mit der Uploadquelle **Videodatei** hinzu. Releases aus diesem Template
erhalten eine Videouploadkonfiguration. Das gilt für manuell erstellte Releases und für Releases aus
Ordnerautomatisierungen oder FTP-Downloads.

## Einschränkungen

- Videouploadkonfigurationen unterstützen keine Linkcrypter. Uploadkonfigurationstemplates mit der Uploadquelle **Videodatei** haben keine Linkcryptertemplates.
- Videouploadkonfigurationen können nicht in Uploadslots von [Releasecollections](/Bearcat/de/release-collections/)
  verwendet werden. Uploadkonfigurationstemplates mit der Uploadquelle **Videodatei** haben keine Felder für Collectionslots.
- Die Uploadquelle ist nach dem Anlegen fest.

## Releaseordner

Videouploads lesen das Hauptvideo aus dem Releaseordner. Solange ein Release eine
Videouploadkonfiguration hat:

- Bearcat behält den Releaseordner. Die [automatische Umwandlung in Unmanaged](/Bearcat/de/advanced-configuration/#releases-automatisch-in-unmanaged-umwandeln)
  und das [Löschen der Rohdateien von Remotedownloads](/Bearcat/de/remote-downloads/#rohdateien-nach-dem-upload-löschen) überspringen das Release.
- **In Unmanaged umwandeln** wird abgelehnt.
- Der Inhaltstyp muss **Film** oder **Serienfolge** bleiben.

Der Inhaltstyp eines Releasetemplates mit einem Uploadkonfigurationstemplate mit der Uploadquelle **Videodatei** muss ebenfalls
**Film** oder **Serienfolge** bleiben.

## Uploads, Onlineprüfungen und Reuploads

Videouploads überspringen die Archiverstellung und kommen direkt in die Uploadwarteschlange. Siehe
[Ablauf eines Uploads](/Bearcat/de/upload-lifecycle/). Onlineprüfungen und Reuploads funktionieren wie bei
Archivuploads.

## Links

Die Releasedetailseite und der Dialog mit den Uploadlinks zeigen den Namen der Videodatei, den Link und den
**Einbettungslink**. Beide Links kannst du kopieren. In der [REST-API](/Bearcat/de/rest-api/) enthält die
Antwort zu Uploadlinks `hosterEmbedLink`.

In der [Postwarteschlange](/Bearcat/de/post-queue/) erscheinen Videouploads unter dem Namen ihrer
Uploadkonfiguration mit der Markierung **Video**.

[Forenpostvorlagen](/Bearcat/de/forum-post-templates/#videouploads) stellen `upload.is_video_upload` und
`upload.embed_links` bereit.

## MixDrop

Lege einen MixDrop-Account mit diesen Feldern an:

| Feld | Wert |
| --- | --- |
| **EmailAddress** | Die E-Mail-Adresse deines MixDrop-Accounts. |
| **ApiKey** | Der API-Schlüssel aus den API-Einstellungen deines MixDrop-Accounts. |
