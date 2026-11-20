---
title: "Releases und Collections automatisch in Foren posten"
description: "Releases und Collections mit Postingregeln automatisch in passende Unterforen posten."
---

Mit Postingregeln wählt Bearcat ein Unterforum, füllt eine Vorlage aus und sendet den Post ab.
Klicke dafür auf **Posten** in der [Postwarteschlange](/Bearcat/de/post-queue/) oder aktiviere das automatische Posten.
Wenn du Entwürfe selbst prüfen und absenden willst, lies [In Foren posten](/Bearcat/de/posting-to-forums/).

Jedes Forum hat eigene Regeln. Bearcat überspringt Foren ohne passende Regel.

## Voraussetzungen

- Ein Forum unter **Registrierungen > Verteilungsseitenregistrierungen** mit funktionierendem Login.
- Eine passende [Forenpostvorlage](/Bearcat/de/forum-post-templates/).
- Releases in der [Postwarteschlange](/Bearcat/de/post-queue/) mit Klassifizierung und bestandener oder manuell freigegebener
  [Qualitätsprüfung](/Bearcat/de/quality-gates/). Gruppen ohne Qualitätsprofil bestehen die Prüfung.
  Bei Collections muss jedes Release die Prüfung bestehen oder manuell freigegeben sein. Mindestens eines muss klassifiziert sein.

## Postingregeln öffnen

Öffne **Registrierungen > Verteilungsseitenregistrierungen** in der Seitenleiste. Die Spalte **Postingregeln** zeigt,
wie viele Regeln das jeweilige Forum hat.

![distribution-site-rules.png](../images/distribution-site-rules.png)

Klicke auf die Zahl, um die Seite zu öffnen, oder nutze **Postingregeln** im Zeilenmenü.

![posting-rules-page.png](../images/posting-rules-page.png)

## Einstellungen pro Forum

**Automatisches Posten** ist standardmässig aus. Du kannst Regeln trotzdem in der Vorschau prüfen und **Posten** in der Postwarteschlange nutzen.
Ist es aktiviert, läuft die Hintergrundaufgabe "Automatic forum posting" standardmässig alle 15 Minuten.
Sie postet zuerst passende Releases aus der Warteschlange, danach Collections.
Das Intervall änderst du unter [Hintergrundaufgaben](/Bearcat/de/advanced-configuration/#hintergrundaufgaben).

**Themensuche ohne Punkte** ist standardmässig an. Bearcat ersetzt Punkte durch Leerzeichen, wenn es
nach Themen sucht und neue Themen benennt. Bei Collections verwendet es den Collectionnamen.
Schalte die Einstellung aus, um die Punkte bei der Suche und in neuen Themennamen zu behalten.

**Mindestabstand zwischen Posts in Sekunden** steht standardmässig auf 30. Die Hintergrundaufgabe wartet mindestens so lange
zwischen zwei Posts oder Aktualisierungen im selben Forum, auch nach Fehlversuchen. Wenn du selbst auf **Posten** oder **Post aktualisieren** klickst, entfällt die Wartezeit.

## Hoster, Linkcrypter und Bildhoster

Auf der Seite **Postingregeln** legst du unter den Einstellungen pro Forum fest, welche Hoster, Linkcrypter und Bildhoster
im Post vorkommen dürfen. Diese Hosterregeln gelten für automatische Posts und manuelle Entwürfe.

Für jede der drei Kategorien gibt es zwei Listen:

- **Erforderlich**: Der Post muss mindestens einen der gewählten Anbieter enthalten.
- **Verboten**: Bearcat entfernt Uploads, Container und Bilder dieser Anbieter aus dem Post.

Eine leere Liste schränkt die Auswahl nicht ein. Ist eine Anforderung unter **Erforderlich** nicht erfüllt oder wurden
alle Uploads, alle Container oder alle Bilder entfernt, überspringt Bearcat das Forum beim automatischen Posten.

Bearcat prüft alle Uploads, Uploadslots und enthaltenen Releases zusammen.
Container mit mehreren Hostern, etwa für alle Hoster eines Releases oder für Uploadslots einer Collection,
prüft Bearcat nur anhand der Linkcrypterlisten. Verbotene Hoster innerhalb solcher Container werden nicht herausgefiltert.

Die Vorlagenvariable `imagelinks.<config>` ist leer, wenn der zugehörige Bildhoster verboten ist.
Prüfe den Wert mit `if` oder verwende für dieses Forum eine eigene Vorlage.

Für übersprungene Posts gibt es keine Benachrichtigung. Klicke in der Postwarteschlange auf **Posten**, um den Grund zu sehen.
Bei einem [manuellen Forenentwurf](/Bearcat/de/posting-to-forums/) erscheint eine Warnung. Du kannst den Entwurf trotzdem posten.

## Regel hinzufügen

Klicke auf **Neue Postingregel**. Fülle die folgenden Felder aus:

- **Gilt für**: wähle oben im Dialog **Release** oder **Collection**. Jede Regel gilt für einen Typ.
- **Name**: dein eigener Name für die Regel
- **Unterforum**: das Ziel des Posts. Klicke auf **Unterforen laden** und wähle eines aus.
  Das Laden kann einige Sekunden dauern.
- **Präfix**: in manchen Unterforen kannst du Präfixe für neue Themen setzen
- **Forenpostvorlage**: die Vorlage für den Posttext. Releaseregeln verwenden Releasevorlagen,
  Collectionregeln verwenden Collectionvorlagen. Die Vorlage bestimmt den Regeltyp.
- **Postingmodus**: entweder *Auf bestehendes Thema antworten, sonst neues Thema* oder *Immer ein neues
  Thema erstellen*.
- **Aktiviert**: deaktivierte Regeln werden beim Abgleich übersprungen

![add-or-edit-rule.png](../images/add-or-edit-rule.png)

## Bedingungen

Mit Bedingungen legst du fest, für welche Releases oder Collections die Regel gilt. Collectionregeln
verwenden dieselben Felder ausser **Episode**. Wechselst du zu **Collection**, werden Bedingungen für Episoden entfernt.
Du kannst Vergleiche gruppieren:

- **Alle (UND)**: jeder Eintrag der Gruppe muss passen.
- **Mindestens eine (ODER)**: mindestens ein Eintrag muss passen.
- **NICHT**: passt, wenn die einzelne Bedingung darin nicht passt.

Gruppen lassen sich verschachteln, so kannst du etwa "Deutsch und (1080p oder 2160p) und nicht von
Gruppe X" abbilden. Mit **Bedingung**, **Gruppe** und **NICHT** fügst du innerhalb einer Gruppe Einträge hinzu.

Jede Vergleichszeile hat ein Feld, einen Operator und einen Wert:

| Feld | Werte |
| --- | --- |
| Releasename | Release- oder Collectionname, Freitext |
| Auflösung | Aus der Klassifizierung, zum Beispiel `R1080p` oder `R2160p` |
| Hauptsprache | Sprache des Releases oder der Collection, zum Beispiel `German` |
| Mehrsprachig | Ja oder nein |
| Inhaltstyp | `Movie`, `TvShowEpisode` oder `Other` |
| Quelle | Zum Beispiel `BluRay` oder `WebDl` |
| Plattform | Aus der Klassifizierung |
| Releasegruppe | Die Releasegruppe in Bearcat, zu der das Release oder die Collection gehört |
| Releasegruppenkürzel | Das Gruppenkürzel aus dem Releasenamen, zum Beispiel `FLAME` |
| Jahr | Zahl |
| Staffel | Zahl |
| Episode | Zahl, nur für Releases |
| Genre | Genres aus der Mediendatenbank, kommagetrennt, zum Beispiel `Animation, Familie` |

Welche Operatoren verfügbar sind, hängt vom Feld ab:

| Feld | Operatoren |
| --- | --- |
| Releasename | *ist*, *ist nicht*, Mustervergleich und *passt auf Regex* |
| Releasegruppe, Releasegruppenkürzel, Genre | *ist*, *ist nicht*, Mustervergleich, *ist eines von*, *ist keines von*, *ist gesetzt*, *ist nicht gesetzt* |
| Hauptsprache | *ist*, *ist nicht*, *ist eines von*, *ist keines von*, *ist gesetzt*, *ist nicht gesetzt* |
| Auflösung, Jahr, Staffel, Episode | Zusätzlich *ist mindestens* und *ist höchstens* |

Bei *passt auf Muster (%)* steht `%` für beliebigen Text. Das Muster muss auf den ganzen Wert passen:

- `%German%` passt auf Namen, die `German` enthalten.
- `%-FLAME` passt auf Namen, die auf `-FLAME` enden.
- `%Animation%` beim Genre passt auf jedes Release, das `Animation` unter seinen Genres hat.

Muster und reguläre Ausdrücke ignorieren Gross- und Kleinschreibung. Ist ein regulärer Ausdruck ungültig
oder überschreitet der Abgleich das Zeitlimit, gilt die Bedingung als nicht erfüllt.

Die meisten Werte stammen aus der Klassifizierung. Bearcat ermittelt sie aus dem Releasenamen und vorhandenen Mediendaten.
Ohne Klassifizierung greifen Releaseregeln nicht.

Bei Collections stammen Name, Inhaltstyp, Releasegruppe und Sprache aus der Collection selbst.
Fehlen Sprache oder Inhaltstyp, übernimmt Bearcat die Angaben aus den enthaltenen Releases.
Auflösung, Quelle, Plattform, Jahr, Staffel, Mehrsprachigkeit und Releasegruppenkürzel stammen aus deren
Klassifizierungen. Bearcat übernimmt einen Wert nur, wenn er bei allen klassifizierten Releases gleich ist.
Sonst gilt er als unbekannt oder nicht gesetzt. Releases ohne Klassifizierung zählen dabei nicht mit.
Das Genre stammt aus den Mediendaten der enthaltenen Releases und gilt nur, wenn es bei allen Releases mit
Genre gleich ist.

## Regeln sortieren

Bearcat verwendet die erste passende Regel für den jeweiligen Typ: Release oder Collection.
Beide Regeltypen stehen in derselben Liste. Ein Badge unter dem Vorlagennamen zeigt den Typ.
Ziehe Regeln am Griff links in die gewünschte Reihenfolge. Setze spezifische Regeln vor allgemeine.

## Probelauf

Speichere deine Regeln. Wähle unter **Probelauf** unten auf der Seite **Releases** oder **Collections**
und klicke auf **Vorschau**. Die Vorschau zeigt für aktuelle Einträge die passende Regel und das Unterforum
oder **Kein Treffer**. Dabei wird nichts gepostet.

Prüfe Änderungen an Bedingungen oder der Regelreihenfolge in der Vorschau, bevor du das automatische Posten aktivierst.

![rule-dry-run.png](../images/rule-dry-run.png)

## Aus der Postwarteschlange posten

Jeder Eintrag unter **Einzelreleases** und **Collections** in der [Postwarteschlange](/Bearcat/de/post-queue/) hat einen Abschnitt
**Automatisches Posten**, der pro Forum zeigt, was die Regeln tun würden:

- die passende Regel und ihr Unterforum, mit einem Button **Posten**,
- **Gepostet** mit einem Link, wenn das Release oder die Collection bereits in diesem Forum gepostet wurde,
- **Keine passende Postingregel**, wenn keine Regel auf das Release oder die Collection passt.

Das Badge **Auto** zeigt, dass Bearcat in diesem Forum automatisch postet.

**Posten** sendet die ausgefüllte Vorlage je nach Regel als Antwort oder neues Thema ab. Bearcat speichert
die Post-URL unter **Veröffentlichungsorte**. **In alle passenden Foren posten** postet nacheinander in die
übrigen passenden Foren.

![posting-from-post-queue.png](../images/posting-from-post-queue.png)

Sobald Bearcat in allen passenden Foren gepostet hat, markiert es das Release oder die Collection als gepostet
und entfernt den Eintrag aus der Warteschlange. Wenn du selbst postest, verwende **Als gepostet markieren**.

Den gleichen Abschnitt **Automatisches Posten** findest du auch in der Übersicht eines Releases oder einer Collection,
über **Veröffentlichungsorte**. Damit postest du auch Releases und Collections, die nicht mehr in der Warteschlange sind,
zum Beispiel in ein später hinzugefügtes Forum.

## Wenn ein Release oder eine Collection nicht gepostet werden kann

Statt der Forenliste zeigt der Eintrag einen kurzen Grund:

- **Automatisches Posten blockiert: Das Quality Gate wurde nicht bestanden.** Das Release muss die
  [Qualitätsprüfung bestehen oder manuell freigegeben werden](/Bearcat/de/quality-gates/).
  Noch ungeprüfte Releases prüft Bearcat beim nächsten Hintergrundlauf.
- **Automatisches Posten blockiert: Das Release hat keine Klassifizierung.** Bearcat klassifiziert Releases
  alle 30 Minuten und nach dem Auslesen der Mediendaten. Warte auf den nächsten Lauf oder klicke
  beim Release auf **Mediendaten extrahieren**.
- **Automatisches Posten blockiert: Nicht jedes Release dieser Collection hat das Quality Gate bestanden.**
  Jedes Release in der Collection muss die Qualitätsprüfung bestehen oder manuell freigegeben sein.
- **Automatisches Posten blockiert: Kein Release dieser Collection hat eine Klassifizierung.**
  Mindestens ein enthaltenes Release muss klassifiziert sein.
- **Noch kein Forum mit Postingregeln vorhanden.** Keine Forenregistrierung hat eine aktivierte Regel.

Die Hintergrundaufgabe versucht es erneut, sobald das Release oder die Collection diese Voraussetzungen erfüllt.

## Post nach einem Reupload aktualisieren

Nach einem Reupload kann Bearcat bestehende Posts mit den neuen Downloadlinks aktualisieren.
Das gilt auch, wenn du eine neue Folge einer Collection hochlädst. Bearcat aktualisiert den bestehenden Post,
statt eine neue Antwort zu posten.
Dazu füllt es die Vorlage mit den aktuellen Daten und **ersetzt den gesamten Post, einschliesslich deiner Änderungen im Forum**.
Verwende das für Posts mit direkten Downloadlinks statt eines Linkcryptercontainers.

### Aus der Warteschlange aktualisieren

Nach einem Reupload oder dem Upload einer neuen Folge erscheint das Release oder die Collection wieder in der
Postwarteschlange. Bei bereits genutzten Foren steht **Postinhalt ist veraltet** neben dem gespeicherten Link.
Klicke auf **Post aktualisieren**, um den Post zu ersetzen.
Vorlage und Aktualisierungszeit werden mit der URL unter **Veröffentlichungsorte** gespeichert.

Bei Foren mit aktiviertem **Automatisches Posten** aktualisiert die Hintergrundaufgabe Posts automatisch,
sofern eine Vorlage verfügbar ist. Das Release oder die Collection verlässt die Warteschlange, sobald alle Posts und Aktualisierungen abgeschlossen sind.

### Template wählen

Bearcat verwendet die erste verfügbare Vorlage in dieser Reihenfolge:

1. Die Vorlage, die du im Dialog wählst.
2. Die Vorlage, mit der der Post erstellt wurde.
3. Die Vorlage der Regel, die aktuell auf das Release oder die Collection passt.

Bei älteren Posts fehlt möglicherweise die gespeicherte Vorlage. Wähle eine im Dialog oder richte eine passende Postingregel ein.

### Aus den Veröffentlichungsorten aktualisieren

Du kannst einen Post auch unter **Veröffentlichungsorte** bei einem Release oder einer Collection aktualisieren.
Klicke auf den Button zum Aktualisieren und bestätige das Ersetzen.
Der Eintrag muss mit einer Verteilungsseite verknüpft sein; auch von Hand bestätigte Posts lassen sich aktualisieren.

## Bestehendes Thema finden

Im Postingmodus *Auf bestehendes Thema antworten, sonst neues Thema* durchsucht Bearcat das Unterforum und vergleicht die
Thementitel mit dem Release- oder Collectionnamen. Dabei ignoriert Bearcat Themenpräfixe, Gross- und Kleinschreibung sowie Satzzeichen.

- Themen mit genau übereinstimmendem Titel haben Vorrang.
- Gibt es keine genaue Übereinstimmung, sucht Bearcat nach Titeln, die alle Wörter des Namens in derselben Reihenfolge enthalten.
- Kein Treffer: Bearcat erstellt ein neues Thema. Ein Treffer: Bearcat antwortet darin.
- Mehrere Treffer: Bearcat wartet auf deine Auswahl und benachrichtigt dich einmal.
  Klicke im Abschnitt **Automatisches Posten** auf **Posten**. Wähle ein bestehendes Thema oder erstelle ein neues.

## Fehlgeschlagene automatische Posts

Schlägt ein automatischer Post oder eine Aktualisierung fehl, wartet Bearcat vor dem nächsten Versuch.
Die Wartezeit gilt nur für dieses Release oder diese Collection im betroffenen Forum:

| Fehlversuche | Wartezeit bis zum nächsten Versuch |
| --- | --- |
| 1 | 15 Minuten |
| 2 | 1 Stunde |
| 3 | 4 Stunden |
| 4 | 12 Stunden |
| 5 | Kein weiterer automatischer Versuch |

Bearcat zählt Fehlversuche beim Posten und Aktualisieren zusammen. Unter **Automatisches Posten** siehst du den letzten Fehler
und ob Bearcat auf den nächsten Versuch wartet oder das automatische Posten gestoppt hat.

Mit **Posten** oder **Post aktualisieren** kannst du es sofort erneut versuchen.
Gelingt der manuelle Versuch, setzt Bearcat den Zähler zurück. Schlägt er fehl, bleibt der Zähler unverändert.

## Benachrichtigungen

Bearcat benachrichtigt dich nach erfolgreichen Posts und Aktualisierungen.
Bei automatischen Fehlversuchen erhältst du erst nach dem fünften eine Benachrichtigung. Frühere Fehlschläge stehen nur im Log.
Auch wenn du zwischen mehreren passenden Themen wählen musst, benachrichtigt dich Bearcat einmal.

Benachrichtigungen für Posts und Aktualisierungen kannst du separat deaktivieren.
Für Posts, die Bearcat wegen der Hosterregeln überspringt, gibt es keine Benachrichtigung.

## Veröffentlichungsorte und Duplikate

Bearcat speichert jedes Forum unter **Veröffentlichungsorte** und zeigt **Gepostet**, statt erneutes Posten anzubieten.
Die Hintergrundaufgabe aktualisiert dort nur veraltete Posts. Geänderte Regeln lösen keinen neuen Post aus.
