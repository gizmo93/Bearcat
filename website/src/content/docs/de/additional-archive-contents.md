---
title: "Zusätzliche Archivinhalte"
description: "Eigene Dateien, Ordner oder Textdateien hinzufügen, wenn Bearcat ein Release packt."
---

Du kannst eigene Dateien, Ordner oder Textdateien in deine Archive aufnehmen, zum Beispiel eine
`Premium.txt` mit deinem Referrallink.

Das geht nur bei [Managed Releases](/Bearcat/de/release-types/), da Bearcat Unmanaged Releases nicht packt.

## Einen Eintrag erstellen

Öffne in der Navigation **Konfiguration > Releaseeinstellungen > Zusätzliche Archivinhalte**.
Du findest die Seite auch mit **Ctrl+K** oder **Cmd+K**.

![additional-archive-content-page.png](../images/additional-archive-content-page.png)

Klicke auf **Neuer zusätzlicher Archivinhalt**, gib dem Eintrag einen eindeutigen Namen und wähle einen Typ.

### Datei oder Ordner

Gib den vollständigen **Quellpfad** zu einer Datei oder einem Ordner auf dem Rechner ein, auf dem
Bearcat läuft. Ordner werden mit ihrem Inhalt kopiert und behalten ihren Namen.

Mit **Durchsuchen** wählst du einen Pfad innerhalb deiner Arbeitsverzeichnisse. Pfade ausserhalb dieser
Verzeichnisse kannst du von Hand eingeben.

In Docker gibst du den Pfad innerhalb des Containers an. Die Datei oder der Ordner muss dort über
einen Bind Mount erreichbar sein.

### Textdatei

Gib einen **Dateinamen** wie `Premium.txt` und den **Textinhalt** ein und speichere.

- Verwende einen Dateinamen ohne Ordner. `__nonce.txt` ist für Bearcat reserviert.
- Der Textinhalt darf nicht leer sein.
- Leerzeichen bleiben erhalten, auch bei ASCII-Art.
- Bearcat schreibt die Datei als UTF-8 mit Windows-Zeilenenden (CRLF).

![additional-archive-content-enter-text-file.png](../images/additional-archive-content-enter-text-file.png)

## Einträge Archiven zuweisen

Damit ein Eintrag ins Archiv kommt, wähle ihn in der Archivkonfiguration unter
**Zusätzliche Archivinhalte** aus:

- **In einem Releasetemplate:** gilt für neue Releases, die aus diesem Template erstellt werden.
- **In einem Release unter Archivkonfigurationen:** gilt nur für dieses Release.

Du kannst mehrere Einträge auswählen und einen Eintrag in mehreren Templates und Releases verwenden.

Jeder ausgewählte Eintrag braucht einen eigenen Datei- oder Ordnernamen. Zum Beispiel kollidiert
`/extras/Premium.txt` mit einer Textdatei namens `premium.txt`. Bearcat ignoriert bei dieser Prüfung
die Gross- und Kleinschreibung.

![additional-archive-content-assign-to-archive-config.png](../images/additional-archive-content-assign-to-archive-config.png)

## Wo die Inhalte im Archiv liegen

Die Inhalte liegen im Archiv direkt im Releaseordner, neben den Releasedateien:

```text
Rel.Name/
  rel.name.mkv
  rel.name.nfo
  Premium.txt
```

Ist **Releaseordner als Stammordner packen** in der Archivkonfiguration deaktiviert, liegen die
Releasedateien und die Inhalte stattdessen auf der obersten Ebene des Archivs.

Bearcat fügt die Inhalte zum Packen in den Releaseordner ein und entfernt sie danach wieder.
Es entfernt auch die `__nonce.txt`, falls **Noncedatei erstellen** aktiviert ist. Das Aufräumen erfolgt
auch bei fehlgeschlagenem Packen oder beim nächsten Start nach einem Absturz.
Deine ursprünglichen Releasedateien bleiben erhalten.

## Wenn das Packen fehlschlägt

Bei diesen Problemen bricht Bearcat das Packen ab und erstellt eine Fehlerbenachrichtigung:

| Problem | Was du prüfen solltest |
| --- | --- |
| Eine Quelldatei oder ein Quellordner fehlt | Prüfe den Quellpfad und gegebenenfalls den Docker Bind Mount. |
| Der Releaseordner enthält bereits denselben Namen | Benenne die zusätzliche Datei oder den Ordner um oder entferne die Zuweisung. Bearcat überschreibt nie deine vorhandenen Dateien. |
| Eine Datei oder ein Ordner kann nicht kopiert werden | Prüfe, ob Bearcat die Quelle lesen und in den Releaseordner schreiben darf. |

## Einen Eintrag bearbeiten oder löschen

Änderungen gelten nur für neu erstellte Archive.

Um einen Eintrag zu löschen, entferne zuerst seine Zuweisungen. Wird er noch verwendet, listet Bearcat die
Templates und Releases auf, die das Löschen verhindern.
