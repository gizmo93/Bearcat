---
title: "Bestehende Linkcrypterordner importieren"
description: "Releases aus FileCrypt, Hide.cx oder ToLink importieren und vorhandene Links übernehmen."
---

Bearcat übernimmt gültige Mirrors als abgeschlossene Uploads. Gibt es ein Release bereits, werden die Ordner dort hinzugefügt. Bei neuen Releases lädt Bearcat die Dateien ausserdem zu den übrigen Hostern des Templates hoch.

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

Nach der Dateiprüfung sind alle importierbaren Ordner automatisch ausgewählt. Das Häkchen neben einem Release wählt alle zugehörigen Ordner aus, und **Alle Treffer auswählen** umfasst alle Seiten der gefilterten Liste. Änderungen an Template oder Primärsprache wirken sich auf alle ausgewählten Treffer aus. **Import starten** übernimmt auch ausgewählte importierbare Ordner, die ein Filter gerade ausblendet.

### Template und Sprache

Standardmässig nutzt Bearcat das **Standardtemplate**. Hast du ein **Serientemplate** gewählt, greift es bei mehreren Releases im selben Ordner, einer Staffelnummer im Releasenamen oder wenn der Name zum Folgenmuster des Templates passt.

Die **Primärsprache** liest Bearcat aus dem Releasenamen, bei Ordnern mit mehreren Releases bei Bedarf auch aus dem ersten Archivnamen. Liefert keiner der beiden Namen eine Sprache, gilt die Standardsprache. Template und Sprache kannst du in der Vorschau ändern.

## Mirrors und lokale Dateien

Für jeden Mirror ist eine Uploadkonfiguration beim passenden Hoster nötig (bei neuen Releases kommt sie aus dem Template). Ein Mirror ist gültig, wenn alle Dateien online sind und ein unterstütztes Archivformat haben (`.rar`, `.r00`, `.7z`, `.7z.001`). Weitere Mirrors müssen genau dieselben Dateinamen haben wie der erste übernommene Mirror.

Ohne passenden Hoster kann Bearcat den Ordner nicht importieren. Ein neues Release braucht zudem entweder einen lokalen Releaseordner oder einen gültigen Mirror. Fehlt der Releaseordner, ist ausserdem ein vorhandenes Archiv oder ein Archivbasisordner nötig.

Mit einem Managed Template sucht Bearcat in den Arbeitsverzeichnissen und Ordnerautomatisierungen nach einem freien Ordner mit dem Releasenamen. Findet es keinen, erstellt es stattdessen ein Unmanaged Release. Vorhandene Archive sucht Bearcat in den Archivspeicherordnern, den Arbeitsverzeichnissen, dem Archivbasisordner und deren direkten Unterordnern. Stimmen die Dateinamen genau mit dem ersten übernommenen Mirror überein, wird das Archiv direkt wiederverwendet.

## Bestehende Releases

Bearcat findet bestehende Releases anhand des Namens in der Vorschau (unabhängig von Gross- und Kleinschreibung). Hat ein Mirror dieselben Links wie ein vorhandener Upload, werden beide verknüpft. Gibt es beim Hoster bereits einen neueren Upload, übernimmt der Container dessen Links beim nächsten Update.

Hat das Release für einen Hoster bereits einen Container desselben Crypteraccounts, schlägt der Import des Ordners fehl.

## Staffelordner

Enthält ein Mirror mehrere Folgen, legt Bearcat anhand der Archivnamen ein Release pro Folge an. Dazu muss die [Collectionerkennung](/de/release-collections/#wie-eine-collection-entsteht) im Template aktiv sein und alle Folgen derselben Collection zuordnen.

Liegen die Archive mehrerer Folgen im selben lokalen Ordner, verschiebt Bearcat die Archivteile in einen Unterordner pro Folge. Solche Verschiebungen stehen in der Vorschau und in der Abschlussmeldung.

Der Crypterordner wird zum Container des [Uploadslots](/de/release-collections/#uploadslots) der Collection, und alle Mirrors müssen zum selben Slot gehören. Mirrors, die sich keiner Folge zuordnen lassen, werden übersprungen.

Der Import schlägt fehl, wenn der Slot schon einen anderen Container desselben Crypteraccounts hat oder ein Release der Collection diesen Account für eigene Container nutzt. Beim Import einer einzelnen Folge kann Bearcat die Links direkt in den vorhandenen Collectioncontainer desselben Accounts übernehmen.

## Nach dem Import

Ein Ordner mit einem Mirror wird zum [Linkcryptercontainer](/de/upload-lifecycle/#5-linkcryptercontainer) des passenden Hosters. Ein Ordner mit mehreren Mirrors wird zum Container für alle Hoster des Releases.

Releaseinformationen werden im Hintergrund abgerufen. Neue Releases verwenden die Sprache aus der Vorschau, bestehende Releases behalten ihre bisherige Sprache. Importierte Releases und Collections gelten als bereits gepostet und erscheinen nicht in der [Postwarteschlange](/de/post-queue/).

## Archivordner nachträglich zuordnen

Fehlende lokale Archive erhalten den Status **Gelöscht**. Bearcat kann sie bei Bedarf aus den Mirrors wiederherstellen. Sind die Dateien bereits auf der Festplatte, kannst du sie zuordnen:

1. Wähle unter **Uploads und Archive > Archive** die betroffenen Archive aus.
2. Klicke auf **Archivordner ändern** und wähle einen Basispfad.
3. Prüfe die gefundenen Ordner und bestätige mit **Archivordner ändern**.

   ![Gefundene Archivordner](../images/link-crypter-import-change-archive-folders-dialog.png)

Bearcat sucht auch in Unterordnern. Die Zuordnung gilt für Archive mit dem Status **Gelöscht** oder **Dateien fehlen**, sofern es kein neueres Archiv gibt. Für jedes Archiv braucht Bearcat genau einen freien Ordner, der nur dessen Archivteile enthält. Bei einem Managed Release muss dieser Ordner ausserhalb des Releaseordners liegen. Nach der Zuordnung erhält das Archiv den Status **Erstellt**.
