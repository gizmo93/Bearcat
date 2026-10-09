---
title: "Zusammengehörige Releases in Collections gruppieren"
description: "Uploadeinstellungen und Containerlinks für zusammengehörige Releases teilen."
---

Eine Releasecollection fasst zusammengehörige Releases zusammen, zum Beispiel die Folgen einer TV-Staffel.

## Wozu eine Collection dient

Lege Hoster, Archiveinstellungen und Passwortrichtlinien für die ganze Collection fest und sammle
ihre Downloadlinks in einem Linkcryptercontainer.

## Wie eine Collection entsteht

Aktiviere **Collectionerkennung** in einem Releasetemplate, damit Collections automatisch erstellt werden.
Bearcat prüft den Namen jedes neuen Releases aus diesem Template auf eine passende Collection.

Wähle, wie Bearcat Releases gruppiert:

- **Muster für Serienfolgen** liest Namen wie `Show.S01E01` und fasst alles aus derselben
  Serie und Staffel zusammen.
- Mit **Eigenes Regex** beschreibst du ein eigenes Muster, wenn deine Namen nicht dem üblichen
  Serienschema folgen.

Für ein passendes Release erstellt Bearcat eine Collection oder fügt es der bestehenden hinzu.
Du findest sie unter **Releasecollections**.

Um ein Release manuell hinzuzufügen, öffne eine Collection und wähle **Release hinzufügen**. Nur Releases
derselben Releasegruppe können hinzugefügt werden.

## Uploadslots

Ein Uploadslot wendet dieselben Einstellungen auf jedes Release der Collection an. Wähle:

- Einen Namen, etwa "Rapidgator passworded".
- Den Hoster, auf den hochgeladen wird.
- Die Archivkonfiguration für jedes Release.
- Ob Downloads auf Premiumnutzer beschränkt werden, sofern der Hoster das unterstützt.
- Eine Passwortrichtlinie für alle Releases im Slot.

Eine Collection kann mehrere Slots haben, zum Beispiel einen pro Hoster.

## Containerlinks

Mit **Linkcrypter für Container bearbeiten** sammelst du die Links eines Slots in einem Container,
etwa für eine ganze Staffel. Passwort und weitere Cryptereinstellungen gelten für alle Uploads im Slot.

## Was eine Collection enthält

![release-collection-detail.png](../images/release-collection-detail.png)

### Kopfbereich

Der Kopfbereich zeigt das Cover, den Serientitel aus der Metadatenquelle, den Collectionschlüssel und die
Serienbeschreibung. **Mehr anzeigen** klappt eine lange Beschreibung aus.

**Im Forum posten** öffnet den [Dialog zum Posten im Forum](/Bearcat/de/posting-to-forums/). Die Schaltfläche ist hervorgehoben,
solange mindestens ein Release einen Hoster online hat und die Collection noch nicht gepostet wurde. Ohne
aktive Forenverteilungsseite wird nur **Forenpost rendern** angezeigt.

Das `...`-Menü enthält **Collectioneinstellungen bearbeiten** für Inhaltstyp und Primärsprache,
**Metadaten bearbeiten** und **Metadaten abrufen**.

Die Statusanzeige zeigt den Fortschritt für Metadaten, Uploads, Linkcontainer, Bilder und Posts.
Rot weist auf ein Problem oder einen offenen Schritt hin. Klicke darauf, um den passenden Tab zu öffnen.

### Serienmetadaten

Bearcat verwendet aktive Metadatenquellen wie TMDB und TheTVDB, um Serientitel,
Beschreibung und Cover zu finden. Wähle **Metadaten abrufen** im `...`-Menü, um nach einer
Änderung von Name oder Sprache erneut zu suchen.

Die Primärsprache bestimmt die Sprache von Titel und Beschreibung. Ohne Auswahl gilt die Standardsprache
des Anbieters. Bearcat sucht über eine IMDb-ID aus den enthaltenen Releases oder über den Titel.

Du kannst die Metadaten in Forenpostvorlagen verwenden und das Cover auf deine Bildhoster hochladen.
Details zur Suche findest du unter
[Releaseinformationen und Metadaten](/Bearcat/de/release-information-and-metadata/).

### Übersicht

**Releases** zeigt jedes Release mit seiner Folgennummer, etwa `E01` oder `S01E01`, wenn die
Collection mehrere Staffeln umfasst. Der Balken zeigt, wie viele Hoster online sind. Über das
`...`-Menü einer Zeile öffnest du das Release oder entfernst es aus der Collection.

**Linkcontainer** listet die Container jedes Slots und die Anzahl enthaltener Uploads.
**Noch nicht erstellt** bedeutet, dass der Crypter keinen Container hat. Fehlgeschlagene Container
kannst du hier löschen. **Alle Links kopieren** kopiert die Container-URLs eines Crypters über alle Slots.

Zur Liste unten auf der Seite siehe [Veröffentlichungsorte](/Bearcat/de/posting-to-forums/#veröffentlichungsorte).

### Uploadslots

![release-collection-upload-slots.png](../images/release-collection-upload-slots.png)

Jede Zeile zeigt die Linkcrypter des Slots, wie viele Releases eine Uploadkonfiguration dafür haben,
und den Zustand seiner Container. Klicke auf eine Zeile, um Passwortrichtlinie, Uploadzahlen und
jeden Container mit seinem Link zu sehen. Slots mit einem fehlgeschlagenen Container sind beim Öffnen der Seite ausgeklappt.

**Linkcrypter für Container bearbeiten** und **Uploadslot löschen** findest du im `...`-Menü einer Zeile.

### Bilder

Lade das Seriencover auf deine Bildhoster hoch, um es in Forenposts zu verwenden. Richte das hier ein:

- **In einem Releasetemplate:** Aktiviere die Collectionerkennung und füge dann Hoster unter **Collectionbilduploadkonfigurationen** hinzu. Sie gelten für die Collections, denen Bearcat die Releases dieses Templates zuordnet.
- **In einer Collection:** Wähle **Hinzufügen** im Tab **Bilder** und wähle einen Hoster.

Lässt du den Namen leer, benennt Bearcat die Konfiguration nach dem Hoster. Einen bestehenden
Eintrag kannst du umbenennen. Für einen anderen Hoster fügst du eine neue Bilduploadkonfiguration hinzu.

Jeder Eintrag zeigt seinen Uploadstatus. Nach dem Upload zeigt er eine Vorschau und Bildlinks
in verschiedenen Grössen. Kopiere die Links einzeln oder gemeinsam, oder verwende `imagelinks`
in Forenpostvorlagen. Details unter
[Forenpostvorlagen](/Bearcat/de/forum-post-templates/).
