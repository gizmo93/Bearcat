---
title: "Forenpostvorlagen"
description: "Forenposts mit Platzhaltern für Releasedaten, Links und Bilder erstellen."
---

Schreibe deinen Post mit Platzhaltern für Releasename, NFO, Links und Bilder.
Bearcat setzt die Daten eines Releases oder einer Collection ein.

Du kannst das Ergebnis von Hand kopieren oder es von [Postingregeln](/Bearcat/de/automatic-forum-posting/) posten lassen.

![Forenpostvorlagen](../images/forum-post-templates-page.png)

## Eine Vorlage erstellen

1. Öffne **Forenpostvorlagen** und klicke auf **+** neben dem Suchfeld.
2. Wähle den Typ: **Release** oder **Releasecollection**. Du kannst ihn später nicht ändern.
3. Wähle das Ausgabeformat. Verwende **BBCode** für Forenposts. Nur BBCode-Vorlagen können in Foren gepostet werden.
4. Gib einen Namen ein, schreibe die Vorlage und drücke **Ctrl+S** (**⌘S** auf einem Mac), um zu speichern.

## Den Editor verwenden

Der Editor hat drei Bereiche: links deine Vorlagen und Variablen, in der Mitte die Vorlage und rechts
eine Livevorschau.

- **Variablen einfügen:** Klicke unter **Verfügbare Variablen** auf eine Variable, um sie an der Cursorposition einzufügen.
  Listen wie `uploads` fügen eine vollständige Schleife ein. Mit dem Suchfeld findest du eine Variable.

  ![Verfügbare Variablen](../images/forum-post-templates-variables-box.png)

- **Vorschau:** Wähle unter **Vorschau mit** ein Release. Die Vorschau aktualisiert sich während des Tippens.
  **Ausgabe** zeigt den fertigen Post, **Daten** zeigt alle Werte dieses Releases.
- **Fehler:** Die Statusleiste zeigt Syntaxfehler. Klicke darauf, um zur Zeile zu springen.

Bleibt ein Platzhalter in der Vorschau leer, prüfe den Tab **Daten**. Er zeigt, was das Release
tatsächlich enthält.

## Vorlagensyntax

Vorlagen verwenden [Scriban](https://scriban.github.io/docs/language/).

Einen Wert ausgeben:

```text
{{ release.name }}
```

Eine Liste durchlaufen:

```text
{{~ for upload in uploads ~}}
[B]{{ upload.name }}[/B]
{{~ end ~}}
```

Mit `~` entfernst du Leerraum um `for` und `end` aus der Ausgabe.
Für Werte innerhalb einer Zeile verwendest du `{{ }}` ohne `~`.

Einen Abschnitt nur anzeigen, wenn der Wert vorhanden ist:

```text
{{~ if release.nfo ~}}
[SPOILER="NFO"]{{ release.nfo }}[/SPOILER]
{{~ end ~}}
```

Ein vollständiges Beispiel mit NFO und Links von Linkcryptern:

```text
[CENTER]
[B]{{ release.name }}[/B]

[SPOILER="NFO"]{{ release.nfo }}[/SPOILER]

{{~ for upload in uploads ~}}
[B]{{ upload.name }}[/B]
{{~ for crypter in upload.link_crypters ~}}
[URL='{{ crypter.container_link }}']{{ crypter.name }}[/URL]
{{~ end ~}}
{{~ end ~}}
[/CENTER]
```

Vorlagen für Collections funktionieren genauso. Sie durchlaufen die Releases der Collection mit
`{{~ for release in releases ~}}`.

## Post rendern

Öffne ein Release oder eine Releasecollection und klicke im Pfeilmenü von **Im Forum posten** auf
**Forenpost rendern**. Ohne aktive Verteilungsseite für ein Forum ist es ein eigener Button. Wähle eine
Vorlage, klicke auf **Forenpost kopieren** und füge das Ergebnis in deinem Forum ein.

![Einen Forenpost rendern](../images/render-template.png)

## Gut zu wissen

- **Leere Werte:** Bei fehlenden Daten bleiben Platzhalter leer. Erstelle den Post erst, wenn Uploads und
  Linkcryptercontainer fertig sind, damit alle Links enthalten sind.
- **Bildlinks:** Verwende `{{ imagelinks.imgbb_cover.full }}` für eine Bilduploadkonfiguration namens
  `ImgBB Cover`. Bearcat wandelt den Namen in Kleinbuchstaben um und ersetzt Leerzeichen durch `_`. Alternativ verwendest du den ursprünglichen Namen: `{{ imagelinks["ImgBB Cover"].full }}`. Der Link erscheint, sobald das Bild hochgeladen ist.
- **Mediendaten:** `release.main_video` und `release.media_files` stammen aus den Videodateien von
  Managed Releases. Bearcat liest sie bei Releases, die aus einem Releasetemplate erstellt wurden,
  automatisch aus. Bei anderen Releases klickst du im Release auf **Mediendaten extrahieren**.
- **Uploadnamen:** Benenne deine Uploadkonfigurationen so, wie sie im Post erscheinen sollen, zum
  Beispiel `Rapidgator`. Dann kannst du `{{ upload.name }}` direkt verwenden.
- **Mehrere Foren:** Die BBCode-Unterstützung unterscheidet sich je nach Forum. Verwende bei Bedarf eine Vorlage pro Forum.
