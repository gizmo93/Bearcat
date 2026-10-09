---
title: "Forenposts vorbereiten und absenden"
description: "Erstelle einen Forenentwurf mit Releasedetails und Downloadlinks, prüfe ihn dann im Browser und sende ihn ab."
---

Bearcat erstellt aus den Releasedaten und deiner Vorlage einen Entwurf für einen Post. Öffne ihn im Browser,
prüfe Titel und Text und sende den Post ab. Du musst dabei mit demselben Forenkonto angemeldet sein.

Damit Bearcat Posts für dich absendet, verwende [Postingregeln](/Bearcat/de/automatic-forum-posting/).

## Voraussetzungen

- Eine Verteilungsseite, also ein Forenkonto, das du in Bearcat hinzugefügt hast.
- Eine [Forenpostvorlage](/Bearcat/de/forum-post-templates/).
- Ein Release mit abgeschlossenen Uploads und Downloadlinks.

## Eine Verteilungsseite einrichten

Öffne **Verteilungsseiten** in der Seitenleiste und füge eine hinzu. Wähle das Forum, gib Benutzername und
Passwort ein und speichere. Dein Passwort wird verschlüsselt gespeichert. Aktiviere die Seite und prüfe mit **Login testen**,
ob die Anmeldung funktioniert.

Wähle für boerse.cx und data-load.me den jeweiligen Foreneintrag.

Für andere XenForo-Foren wählst du **XenForo**. Gib Benutzername, Passwort und die Adresse der Forenstartseite
ein, einschliesslich eines Installationsordners, zum Beispiel `https://example.org/community/`. Verwende die
Adresse, die dein Browser nach Weiterleitungen anzeigt. Füge jedes Forum einzeln hinzu.

Der XenForo-Eintrag braucht Friendly URLs und die Standardformulare von XenForo für Login, Post und Entwurf.
Routing über `index.php`, SSO, CAPTCHA und Zwei-Faktor-Login werden nicht unterstützt. Eigene Themes,
Pflichtfelder für Threads oder Add-ons können das Posten verhindern. Teste den Login und bereite einen Entwurf vor,
bevor du automatisches Posten aktivierst.

## Ein Release posten

Öffne ein Release und klicke im Kopfbereich auf **Im Forum posten**. **Forenpost rendern** findest du im Pfeilmenü
derselben Schaltfläche.

![Schaltfläche Im Forum posten](../images/post-to-forum-button.png)

Im Dialog:

1. Wähle die Verteilungsseite, auf der du posten willst.
   ![distribution-site-selection.png](../images/distribution-site-selection.png)
2. Suche und wähle ein Unterforum. Du kannst auch den Releasenamen bearbeiten, mit dem Bearcat
   nach bestehenden Threads sucht und neue benennt. **Punkte → Leerzeichen** ersetzt Punkte durch Leerzeichen
   und setzt Leerzeichen um den letzten Bindestrich vor der Releasegruppe.
   ![subforum-selection.png](../images/subforum-selection.png)
3. Bearcat sucht im Unterforum nach einem Thread, der zum Namen passt. Findet es einen, schlägt Bearcat
   eine Antwort vor. Sonst schlägt es einen neuen Thread vor.

   <figure>

   ![Bestehender Thread gefunden](../images/existing-thread-found.png)

   <figcaption>Bestehender Thread gefunden</figcaption>
   </figure>

   <figure>

   ![Kein bestehender Thread gefunden](../images/no-existing-thread-found.png)

   <figcaption>Kein bestehender Thread gefunden</figcaption>
   </figure>

4. Wähle eine Forenpostvorlage. Bearcat füllt sie mit den Releasedaten aus. Den fertigen Text
   kannst du noch bearbeiten. Für einen neuen Thread legst du auch den Titel und gegebenenfalls ein Präfix wie 1080p oder x265 fest.

   <figure>

   ![add-to-existing-topic.png](../images/add-to-existing-topic.png)

   <figcaption>Antwort auf einen bestehenden Thread vorbereiten</figcaption>
   </figure>

   <figure>

   ![create-new-thread.png](../images/create-new-thread.png)

   <figcaption>Neuen Thread vorbereiten</figcaption>
   </figure>

5. Klicke auf **Entwurf vorbereiten**. Bearcat speichert den Entwurf im Forum und zeigt einen Link **Entwurf im
   Forum öffnen**.

   ![draft-created.png](../images/draft-created.png)

## Den Post absenden

1. Klicke auf **Entwurf im Forum öffnen** in einem Browser, der mit demselben Forenkonto angemeldet ist. Sonst
   zeigt der Editor den Entwurf nicht an.
2. Prüfe Titel und Text, verwende bei Bedarf die Vorschau des Forums und sende den Post ab.
3. Klicke zurück in Bearcat auf **Ich habe gepostet**, um die URL des Posts unter **Veröffentlichungsorte** zu speichern.
   Findet Bearcat den Post noch nicht, füge seine URL ein. Es kann einige Sekunden dauern, bis die Forensuche ihn findet.

<figure>

![entry-draft-in-forum.png](../images/entry-draft-in-forum.png)

<figcaption>Antwortentwurf in einem bestehenden Thread</figcaption>
</figure>

<figure>

![new-topic-draft-in-forum.png](../images/new-topic-draft-in-forum.png)

<figcaption>Entwurf eines neuen Threads</figcaption>
</figure>

**Ich habe gepostet** speichert die URL, entfernt das Release aber nicht aus der
[Postwarteschlange](/Bearcat/de/post-queue/). Verwende dort **Als gepostet markieren**, wenn du mit dem Posten fertig bist.

## Veröffentlichungsorte

**Veröffentlichungsorte** listet die URLs, unter denen ein Release oder eine Collection veröffentlicht wurde,
etwa Forenthreads oder WordPress-Seiten. Das Bestätigen eines Forenposts fügt dessen URL hier hinzu.
Du kannst Links auch von Hand hinzufügen oder entfernen.
