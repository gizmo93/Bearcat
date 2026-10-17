---
title: "Videohoster"
description: "Videos direkt zu DoodStream, MixDrop, Playmate, Streamtape, Vidara, Videro oder Vixeo hochladen, ohne sie vorher zu packen."
---

Bearcat kann die Hauptvideodatei eines Releases direkt zu einem Videohoster hochladen, ohne sie vorher
zu packen. Wähle dafür in der Uploadkonfiguration die Uploadquelle **Videodatei**.

Nach dem Upload erhältst du einen Dateilink und einen Einbettungslink. Mit dem Einbettungslink kannst
du den Videoplayer auf einer anderen Seite einbinden.

## Unterstützte Videohoster

- DoodStream
- MixDrop
- Playmate
- Streamtape
- Vidara
- Videro
- Vixeo

## Voraussetzungen

Videouploads sind nur für [Managed Releases](/Bearcat/de/release-types/) mit dem Inhaltstyp **Film** oder
**Serienfolge** möglich. Dasselbe gilt für Releasetemplates: Sie müssen managed sein und einen dieser
Inhaltstypen haben.

## Hauptvideo

Bearcat wählt anhand der extrahierten Mediendaten die grösste Datei mit einem Videostream und lädt nur
diese hoch. Fehlen die Mediendaten oder hat keine Datei einen Videostream, schlägt der Upload fehl.

## Videouploadkonfiguration anlegen

1. Öffne die Releasedetailseite und füge eine Uploadkonfiguration hinzu.
2. Wähle unter **Uploadquelle** die Option **Videodatei**. Das Feld erscheint nur, wenn das Release die
   Voraussetzungen für Videouploads erfüllt.
3. Wähle als **Uploadziel** einen Account oder einen [Accountpool](/Bearcat/de/account-pools/) eines Videohosters.

Bei **Videodatei** brauchst du keine Archivkonfiguration. Als Uploadziele stehen nur Accounts und Pools
von Videohostern zur Auswahl. Bei **Archivdateien** siehst du nur die übrigen Hoster.
Die Uploadquelle kannst du später nicht mehr ändern.

## Releasetemplates

Füge im [Releasetemplate](/Bearcat/de/release-templates-and-automations/) ein Uploadkonfigurationstemplate
hinzu und wähle als Uploadquelle **Videodatei**. Alle Releases aus diesem Template übernehmen die
Videouploadkonfiguration, auch bei Ordnerautomatisierungen und FTP-Downloads.

## Einschränkungen

- Videouploads unterstützen keine Linkcrypter. In Templates mit der Uploadquelle **Videodatei** kannst
  du deshalb keine Linkcryptertemplates wählen.
- Videouploadkonfigurationen lassen sich keinen Uploadslots von
  [Releasecollections](/Bearcat/de/release-collections/) zuweisen. Die Felder für Collectionslots fehlen
  deshalb auch in den Templates.

## Releaseordner

Für Videouploads braucht Bearcat das Hauptvideo im Releaseordner. Solange das Release eine
Videouploadkonfiguration hat, gilt:

- Bearcat behält den Releaseordner. Das Release wird weder
  [automatisch in Unmanaged umgewandelt](/Bearcat/de/advanced-configuration/#releases-automatisch-in-unmanaged-umwandeln)
  noch werden seine [Rohdateien nach einem Remotedownload gelöscht](/Bearcat/de/remote-downloads/#rohdateien-nach-dem-upload-löschen).
- Du kannst das Release auch nicht manuell **in Unmanaged umwandeln**.
- Der Inhaltstyp muss **Film** oder **Serienfolge** bleiben.

Auch Releasetemplates mit Videouploads müssen den Inhaltstyp **Film** oder **Serienfolge** behalten.

## Uploads, Onlineprüfungen und Reuploads

Videouploads kommen ohne Archiverstellung direkt in die Uploadwarteschlange. Onlineprüfungen und
Reuploads funktionieren wie bei Archivuploads. Siehe [Ablauf eines Uploads](/Bearcat/de/upload-lifecycle/).

## Links

Auf der Releasedetailseite und im Dialog mit den Uploadlinks findest du den Dateinamen, den Dateilink
und den **Einbettungslink**. Beide Links kannst du kopieren. Die [REST-API](/Bearcat/de/rest-api/) gibt den
Einbettungslink im Feld `hosterEmbedLink` zurück.

In der [Postwarteschlange](/Bearcat/de/post-queue/) stehen Videouploads unter dem Namen ihrer
Uploadkonfiguration und sind mit **Video** markiert.

In [Forenpostvorlagen](/Bearcat/de/forum-post-templates/#videouploads) kannst du `upload.is_video_upload`
und `upload.embed_links` verwenden.
