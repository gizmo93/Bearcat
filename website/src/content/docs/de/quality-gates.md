---
title: "Releasequalität vor dem Upload prüfen"
description: "Blockiere Uploads, bis ein Release die von dir definierten Qualitätsprüfungen besteht."
---

Bearcat erstellt erst Uploads, wenn ein Release die festgelegten Qualitätsprüfungen besteht oder du es manuell freigibst.
So erkennst du fehlende NFOs, Coverbilder oder unvollständige Releaseordner vor dem Upload.

[Unmanaged Releases](/Bearcat/de/release-types/) haben keinen Releaseordner, deshalb läuft für sie nur
die Prüfung **Erforderliche Releaseinfos**. Gruppen ohne Qualitätsprofil bestehen die Qualitätsprüfung.

## Qualitätsprofile und Releasegruppen

- Ein **Qualitätsprofil** legt Prüfungen fest, etwa "NFO vorhanden" und "Ordner mindestens 100 MB".
- Weise einer **Releasegruppe** ein Profil zu, damit dessen Prüfungen für alle Releases der Gruppe gelten.

Du kannst dasselbe Profil mehreren Releasegruppen zuweisen.

## Ein Qualitätsprofil erstellen

Öffne **Qualitätsprofile** in der Gruppe **Konfiguration** der Seitenleiste und klicke auf **Neues Qualitätsprofil**.
Gib einen Namen ein und füge eine oder mehrere Prüfungen hinzu. Jede Prüfung hat eigene Einstellungen:

- **Datei mit Muster vorhanden**: Irgendwo im Releaseordner muss eine Datei existieren, die zum eingegebenen Muster passt
  (zum Beispiel `*.nfo` oder `*.sfv`). Das Muster wird mit den Dateinamen verglichen, Gross- und Kleinschreibung
  spielt keine Rolle.
- **Mindestgrösse des Ordners**: Der Releaseordner muss mindestens die eingestellte Anzahl Megabyte gross sein.
  Bearcat misst den Ordner bei jeder Auswertung der Qualitätsprüfung neu. So werden auch noch laufende Kopiervorgänge berücksichtigt.
- **Erforderliche Releaseinfos**: Schalte die Teile der Releaseinfo ein, die ausgefüllt sein müssen: das
  Coverbild, die Beschreibung und/oder das NFO.
- **MediaInfo vorhanden**: Für das Release müssen Mediendaten extrahiert worden sein.

Die Prüfungen, die den Releaseordner betreffen (Dateimuster, Ordnergrösse, MediaInfo), werden bei
Unmanaged Releases übersprungen.

Ein Release besteht nur, wenn **alle** Prüfungen seines Profils bestanden sind.

![add-edit-quality-profile.png](../images/add-edit-quality-profile.png)

## Einer Releasegruppe ein Profil zuweisen

Öffne die Releasegruppe (unter **Konfiguration**, **Releasegruppen**) und wähle im Feld **Qualitätsprofil** ein
Qualitätsprofil. Lass es leer, wenn die Gruppe keine Prüfungen braucht. Die Liste der Releasegruppen zeigt, welches
Profil jede Gruppe verwendet.

![assign-quality-profile-to-release-group.png](../images/assign-quality-profile-to-release-group.png)

## Was die Qualitätsprüfung bewirkt

Die Prüfungen gelten für den ersten Upload und für [automatische Reuploads](/Bearcat/de/upload-lifecycle/#7-automatische-reuploads).
Schlägt eine Prüfung später fehl, pausieren automatische Reuploads, bis das Release wieder besteht oder du es manuell freigibst.

Jedes Release hat einen aktuellen Prüfstatus:

- **Nicht geprüft**: Die Qualitätsprüfung ist noch nicht gelaufen.
- **Bestanden**: Alle Prüfungen waren erfolgreich.
- **Fehlgeschlagen**: Mindestens eine Prüfung ist fehlgeschlagen, neue Uploads sind blockiert.
- **Manuell freigegeben**: Uploads sind ohne weitere Prüfungen erlaubt.

## Wann Bearcat prüft

Bearcat prüft Releases:

- vor dem Erstellen eines Uploads,
- regelmässig im Hintergrund über die Aufgabe **"Quality gate re-evaluation"** (standardmässig alle 30 Minuten,
  das Intervall änderst du auf der Seite [Hintergrundaufgaben](/Bearcat/de/advanced-configuration/#hintergrundaufgaben)), und
- auf Anforderung, wenn du bei einem Release in der Liste der Qualitätsprobleme auf **Neu prüfen** klickst.

Die Hintergrundaufgabe prüft auch ältere Releases mit fehlgeschlagener oder noch ausstehender Prüfung.

Änderst du die Prüfungen eines Profils oder weist einer Releasegruppe ein anderes Profil zu, gehen die betroffenen
Releases zurück auf **Nicht geprüft** und werden beim nächsten Lauf erneut geprüft.

Manuell freigegebene Releases prüft Bearcat nicht erneut. Sie bleiben freigegeben, bis du die Freigabe änderst.

## Die Liste der Qualitätsprobleme

Klicke auf das Schildsymbol neben der Benachrichtigungsglocke, um Releases mit fehlgeschlagenen Prüfungen zu sehen.
Das Badge zeigt die Anzahl betroffener Releases und verschwindet, wenn keine mehr übrig sind.

![quality-gate-badge.png](../images/quality-gate-badge.png)

Die Liste zeigt die betroffenen Releases, ihre fehlgeschlagenen Prüfungen und den letzten Prüfzeitpunkt. Für jeden Eintrag hast du drei Aktionen:

- **Öffnen** führt zum Release. Ergänze dort fehlende Angaben und klicke auf **Neu prüfen**
  oder warte auf die nächste Hintergrundprüfung.
- **Neu prüfen** wertet das Release sofort erneut aus.
- **Manuell freigeben** markiert das Release als **manuell freigegeben**. Damit sind Uploads trotz fehlgeschlagener Prüfungen erlaubt,
  und weitere Prüfungen entfallen.

![quality-gate-list.png](../images/quality-gate-list.png)

Sobald ein Release besteht (oder du es freigibst), verschwindet es aus der Liste, und seine Uploads werden bei der
nächsten Prüfung des Uploadstatus erstellt.
