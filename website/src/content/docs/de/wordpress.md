---
title: "WordPress"
description: "WordPress als Verteilungsseite einrichten und Releases im Blog posten."
---

Mit Bearcat kannst du Entwürfe in WordPress vorbereiten, Posts über [Postingregeln](/Bearcat/de/automatic-forum-posting/)
veröffentlichen und bestehende Posts aktualisieren. WordPress erscheint unter den Verteilungsseiten als **Blog**.
Bearcat verwendet dafür die REST-API von WordPress.

## Voraussetzungen

- WordPress 5.6 oder neuer. Seit dieser Version gibt es Anwendungspasswörter.
- HTTPS. WordPress bietet Anwendungspasswörter nur über HTTPS oder in einer lokalen Umgebung an.
- Aktivierte Anwendungspasswörter. Manche Sicherheitsplugins deaktivieren sie.
- Ein Konto mit der Rolle Redakteur oder Administrator. **Login testen** schlägt fehl, wenn das Konto
  keine Beiträge veröffentlichen darf. WordPress filtert das HTML von Rollen unterhalb von Redakteur
  (Berechtigung `unfiltered_html`), dabei können Tags aus deiner Vorlage entfernt werden.
- Keine Cloudflare- oder andere Botprüfung vor der Seite.

Bearcat ruft die REST-API über `index.php?rest_route=` auf. Jede Permalinkeinstellung funktioniert.

## Anwendungspasswort erstellen

1. Melde dich in WordPress mit dem Konto an, das Bearcat verwenden soll.
2. Öffne **Benutzer > Profil** und scrolle zu **Anwendungspasswörter**.
3. Gib einen Namen ein, zum Beispiel `Bearcat`, und erstelle das Passwort.
4. Kopiere das Passwort. WordPress zeigt es nur einmal an.

## Verteilungsseite hinzufügen

Öffne **Registrierungen > Verteilungsseitenregistrierungen** und füge eine Seite hinzu. Wähle **WordPress** und fülle die folgenden Felder aus:

| Feld | Wert |
| --- | --- |
| **Adresse** | Adresse des Blogs, zum Beispiel `https://example.org/`. Gib auch den Installationsordner an, falls WordPress in einem Unterordner läuft. |
| **Benutzername** | Dein Benutzername in WordPress |
| **Anwendungspasswort** | Das Anwendungspasswort, nicht das Kontopasswort |
| **Cover als Beitragsbild hochladen** | Standardmässig aus. Siehe [Bilder](#bilder). |

Speichere und klicke auf **Login testen**.

## Ziele

Als **Ziel** wählst du eine Kategorie aus WordPress. Bearcat zeigt auch Unterkategorien an,
zum Beispiel `Filme › Filme HD Deutsch`. Jeder Post und jede Regel hat genau eine Kategorie.

## Posten

[Automatisches Posten](/Bearcat/de/automatic-forum-posting/) veröffentlicht den Post direkt.
Bei Blogs gibt es keine Themen oder Antworten. Deshalb entfallen Themensuche, Präfixe und Postingmodus.

Beim [manuellen Posten](/Bearcat/de/posting-to-forums/) bereitest du einen Entwurf vor und veröffentlichst ihn selbst:

1. Klicke auf **Entwurf vorbereiten**. Bearcat erstellt einen Entwurf in WordPress.
2. Klicke auf **Entwurf öffnen**. Der Link öffnet den Editor von WordPress (`/wp-admin/post.php?post=<ID>&action=edit`).
   Du musst in diesem Browser in WordPress angemeldet sein.
3. Veröffentliche den Post in WordPress.
4. Klicke auf **Ich habe gepostet**. Bearcat speichert `<Adresse>/?p=<ID>` ohne Suche unter **Veröffentlichungsorte**.

**Themensuche ohne Punkte** auf der Seite **Postingregeln** gilt auch für Blogs. Ist die Einstellung an (Standard),
ersetzt Bearcat Punkte im Posttitel durch Leerzeichen.

Beim Aktualisieren ersetzt Bearcat den gesamten Inhalt des Posts.

## Bestehende Posts

Vor dem automatischen Posten sucht Bearcat im Blog nach Posts mit demselben Titel. Posts im Papierkorb zählen nicht.

| Gefundene Posts | Verhalten |
| --- | --- |
| Kein Post | Erstellt einen neuen Post. |
| Ein veröffentlichter Post deines Kontos | Ersetzt den Inhalt und speichert die URL unter **Veröffentlichungsorte**. |
| Ein Post deines Kontos mit anderem Status, zum Beispiel ein Entwurf | Ersetzt den Inhalt, veröffentlicht ihn und speichert die URL unter **Veröffentlichungsorte**. |
| Ein Post eines anderen Autors | Überspringt die Seite und lässt den Post unverändert. |
| Mehrere Posts | Überspringt die Seite und lässt die Posts unverändert. |

Wenn Bearcat einen bestehenden Post aktualisiert, gehen deine manuellen Änderungen am Inhalt verloren. Sein Beitragsbild bleibt unverändert.
Bearcat prüft übersprungene Seiten bei jedem automatischen Lauf erneut.

Beim manuellen Posten erscheint beim Verfassen die Warnung **Auf dieser Seite gibt es bereits einen Post mit diesem Titel.** mit Links zu den Posts.
Du kannst trotzdem einen Entwurf vorbereiten.

## Vorlage

Erstelle eine [Postvorlage](/Bearcat/de/forum-post-templates/) mit dem Ausgabeformat **HTML**.
Beim Posten auf WordPress kannst du nur HTML-Vorlagen auswählen.

Bearcat sendet die gerenderte Vorlage unverändert als Postinhalt. Umschliesse sie mit einem `wp:html`-Block, damit
der Blockeditor das Markup behält, und maskiere Werte mit `| html.escape`:

```text
<!-- wp:html -->
<div class="release-layout">
  {{~ if imagelinks.cover.full ~}}
  <img src="{{ imagelinks.cover.full | html.escape }}" alt="{{ classification.title | html.escape }}">
  {{~ end ~}}
  {{~ if release.nfo ~}}
  <details>
    <summary>NFO</summary>
    <pre>{{ release.nfo | html.escape }}</pre>
  </details>
  {{~ end ~}}
  {{~ for crypter in link_crypters ~}}
  <a href="{{ crypter.container_link | html.escape }}" rel="nofollow noopener">{{ crypter.name | html.escape }}</a>
  {{~ end ~}}
</div>
<!-- /wp:html -->
```

Ein vollständiges Beispiel findest du unter
[`tools/wordpress/release.scriban`](https://github.com/gizmo93/Bearcat/blob/main/tools/wordpress/release.scriban)
im Repository.

## Bilder

Verlinke Bilder deines Bildhosters in der Vorlage mit `imagelinks`.

Ist **Cover als Beitragsbild hochladen** an, übernimmt Bearcat das Cover des Releases oder der Collection
in die Mediathek von WordPress und setzt es als Beitragsbild des neuen Posts oder Entwurfs.
Ohne Cover erhält der Post kein Beitragsbild. Screenshots lädt Bearcat nicht in die Mediathek hoch.

- Das Konto muss Dateien hochladen dürfen. Sonst schlägt **Login testen** fehl.
- Für jeden neuen Post mit Cover lädt Bearcat ein eigenes Bild in die Mediathek hoch.
- Löschst du einen Post in WordPress, bleibt sein Bild in der Mediathek.
- Beim Aktualisieren eines Posts ändert Bearcat das Beitragsbild nicht.

## Nicht unterstützt

- Schlagwörter
- Mehrere Kategorien pro Post
