---
title: "Bestehende Linkcrypterordner importieren"
description: "Releases aus FileCrypt, Hide.cx oder ToLink importieren und vorhandene Links übernehmen."
---

Bearcat übernimmt gültige Mirrors als abgeschlossene Uploads. Gibt es ein Release bereits, fügt Bearcat die Ordner dort hinzu. Bei neuen Releases lädt Bearcat die Dateien auch zu den übrigen Hostern des Templates hoch.

Der Import unterstützt **FileCrypt**, **Hide.cx** und **ToLink**. Bei FileCrypt braucht der API-Schlüssel das Recht `LIST`. Bei ToLink gruppiert Bearcat die Links nach Hosterdomain.

## Ablauf

1. Öffne **Registrierungen > Crypter > Linkcrypterimporte** und klicke auf **Neuer Import**.
2. Wähle die Crypterregistrierung und das **Standardtemplate**. Bei Bedarf kannst du ein **Serientemplate** und eine **Standardsprache** wählen.
3. Fehlen lokale Dateien, wähle den **Archivbasisordner für Releases ohne lokale Dateien**. Dort kann Bearcat die Archive bei Bedarf aus den Mirrors wiederherstellen.

   ![Dialog für einen neuen Import](../images/link-crypter-import-new-import-dialog.png)

4. Klicke auf **Crypterordner lesen**. Bearcat überspringt Ordner mit bereits bekannten URLs.
5. Wähle einen Account pro Hoster und klicke auf **Dateien prüfen**.
6. Prüfe die Auswahl und klicke auf **Import starten**.

## Prüfung

![Importvorschau](../images/link-crypter-import-review.png)

Bearcat gruppiert die Ordner nach Releasename. Mit **Release umbenennen** kannst du den Namen ändern. Gibst du den Namen eines anderen Eintrags ein, führt Bearcat die Ordner zusammen und übernimmt Template und Sprache dieses Eintrags.

Suche nach einem Teil des Release- oder Ordnernamens oder nutze den Statusfilter. Die Suche ignoriert Gross- und Kleinschreibung. Mit `*` kannst du einen beliebigen Teil des Namens abdecken, etwa in `Show.S01*-GRP`.

Nach der Dateiprüfung sind alle importierbaren Ordner ausgewählt. Das Häkchen neben einem Release gilt für alle seine Ordner. **Alle Treffer auswählen** gilt für alle Seiten der gefilterten Liste. Änderungen an Template und Primärsprache gelten für die ausgewählten Treffer. **Import starten** übernimmt auch ausgewählte importierbare Ordner, die gerade ausgeblendet sind.

### Template und Sprache

Bearcat verwendet das **Standardtemplate**. Falls du ein **Serientemplate** gewählt hast, nutzt Bearcat es bei mehreren Releases im selben Ordner, bei einer Staffelnummer im Releasenamen oder wenn der Name zum Folgenmuster des Serientemplates passt.

Bearcat liest die **Primärsprache** aus dem Releasenamen. Bei mehreren Releases im Ordner prüft es bei Bedarf auch den ersten Archivnamen. Wenn beide Namen keine Sprache liefern, gilt die Standardsprache. Template und Sprache kannst du in der Vorschau ändern.

## Mirrors und lokale Dateien

Bearcat braucht für jeden Mirror eine Uploadkonfiguration beim passenden Hoster. Bei neuen Releases kommt sie aus dem Template. Ein Mirror ist gültig, wenn alle Dateien online sind und ein unterstütztes Archivformat haben (`.rar`, `.r00`, `.7z`, `.7z.001`). Weitere Mirrors müssen dieselben Dateinamen haben wie der erste übernommene Mirror.

Ohne passenden Hoster kann Bearcat den Ordner nicht importieren. Für ein neues Release braucht es zudem einen lokalen Releaseordner oder einen gültigen Mirror. Fehlt der Releaseordner, braucht Bearcat ausserdem ein vorhandenes Archiv oder einen Archivbasisordner.

Mit einem Managed Template sucht Bearcat in den Arbeitsverzeichnissen und Ordnerautomatisierungen nach einem freien Ordner mit dem Releasenamen. Ohne passenden Ordner erstellt es ein Unmanaged Release. Vorhandene Archive sucht Bearcat in den Archivspeicherordnern, den Arbeitsverzeichnissen, dem Archivbasisordner und ihren direkten Unterordnern. Stimmen die Dateinamen genau mit dem ersten übernommenen Mirror überein, verwendet Bearcat das Archiv direkt.

## Bestehende Releases

Bearcat findet bestehende Releases anhand des Namens in der Vorschau. Gross- und Kleinschreibung spielen dabei keine Rolle. Hat ein Mirror dieselben Links wie ein vorhandener Upload, verknüpft Bearcat beide. Gibt es beim Hoster einen neueren Upload, übernimmt der Container dessen Links beim nächsten Update.

Hat das Release für einen Hoster bereits einen Container desselben Crypteraccounts, schlägt der Import des Ordners fehl.

## Staffelordner

Enthält ein Mirror mehrere Folgen, legt Bearcat anhand der Archivnamen ein Release pro Folge an. Dazu muss die [Collectionerkennung](/Bearcat/de/release-collections/#wie-eine-collection-entsteht) im Template aktiv sein und alle Folgen derselben Collection zuordnen.

Liegen die Archive mehrerer Folgen im selben lokalen Ordner, verschiebt Bearcat die Archivteile in einen Unterordner pro Folge. Solche Verschiebungen stehen in der Vorschau und in der Abschlussmeldung.

Der Crypterordner wird zum Container des [Uploadslots](/Bearcat/de/release-collections/#uploadslots) der Collection. Die Mirrors müssen zum selben Slot gehören. Bearcat überspringt Mirrors, die sich keiner Folge zuordnen lassen. Der Import schlägt fehl, wenn der Slot schon einen anderen Container desselben Crypteraccounts hat oder ein Release der Collection diesen Account für eigene Container nutzt. Beim Import einer einzelnen Folge kann Bearcat die Links direkt in den vorhandenen Collectioncontainer desselben Accounts übernehmen.

## Nach dem Import

Ein Ordner mit einem Mirror wird zum [Linkcryptercontainer](/Bearcat/de/upload-lifecycle/#5-linkcryptercontainer) des passenden Hosters. Ein Ordner mit mehreren Mirrors wird zum Container für alle Hoster des Releases.

Bearcat ruft die Releaseinformationen im Hintergrund ab. Neue Releases verwenden die Sprache aus der Vorschau. Bestehende Releases behalten ihre Sprache. Importierte Releases und Collections gelten als gepostet und erscheinen nicht in der [Postwarteschlange](/Bearcat/de/post-queue/).

## Archivordner nachträglich zuordnen

Fehlende lokale Archive erhalten den Status **Gelöscht**. Bearcat kann sie bei Bedarf aus den Mirrors wiederherstellen. Sind die Dateien bereits auf der Festplatte, kannst du sie zuordnen:

1. Wähle unter **Uploads und Archive > Archive** die betroffenen Archive aus.
2. Klicke auf **Archivordner ändern** und wähle einen Basispfad.
3. Prüfe die gefundenen Ordner und bestätige mit **Archivordner ändern**.

   ![Gefundene Archivordner](../images/link-crypter-import-change-archive-folders-dialog.png)

Bearcat sucht auch in Unterordnern. Die Zuordnung gilt für Archive mit dem Status **Gelöscht** oder **Dateien fehlen**, sofern es kein neueres Archiv gibt. Für jedes Archiv braucht Bearcat genau einen freien Ordner, der nur dessen Archivteile enthält. Bei einem Managed Release muss dieser Ordner ausserhalb des Releaseordners liegen. Nach der Zuordnung erhält das Archiv den Status **Erstellt**.
