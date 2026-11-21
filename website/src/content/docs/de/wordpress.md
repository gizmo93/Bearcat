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
| **Benutzername** | Der WordPress-Benutzername |
| **Anwendungspasswort** | Das Anwendungspasswort, nicht das Kontopasswort |

Speichere und klicke auf **Login testen**.

## Ziele

Als **Ziel** wählst du eine Kategorie aus WordPress. Bearcat zeigt auch Unterkategorien an,
zum Beispiel `Filme › Filme HD Deutsch`. Jeder Post und jede Regel hat genau eine Kategorie.

## Posten

[Automatisches Posten](/Bearcat/de/automatic-forum-posting/) veröffentlicht den Post direkt.
Bei Blogs gibt es keine Themen oder Antworten. Deshalb entfallen Themensuche, Präfixe und Postingmodus.

Beim [manuellen Posten](/Bearcat/de/posting-to-forums/) bereitest du einen Entwurf vor und veröffentlichst ihn selbst:

1. Klicke auf **Entwurf vorbereiten**. Bearcat erstellt einen Entwurf in WordPress.
2. Klicke auf **Entwurf öffnen**. Der Link öffnet den WordPress-Editor (`/wp-admin/post.php?post=<ID>&action=edit`).
   Du musst in diesem Browser in WordPress angemeldet sein.
3. Veröffentliche den Post in WordPress.
4. Klicke auf **Ich habe gepostet**. Bearcat speichert `<Adresse>/?p=<ID>` ohne Suche unter **Veröffentlichungsorte**.

**Themensuche ohne Punkte** auf der Seite **Postingregeln** gilt auch für Blogs. Ist die Einstellung an (Standard),
ersetzt Bearcat Punkte im Posttitel durch Leerzeichen.

Beim Aktualisieren ersetzt Bearcat den gesamten Inhalt des Posts.

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

Verlinke Bilder deines Bildhosters in der Vorlage mit `imagelinks`. Bearcat lädt keine Bilder in die
WordPress-Mediathek hoch und setzt kein Beitragsbild.

## Nicht unterstützt

- Schlagwörter
- Beitragsbild
- Mehrere Kategorien pro Post
