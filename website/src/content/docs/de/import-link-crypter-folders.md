---
title: "Bestehende Linkcrypterordner importieren"
description: "Releases aus bestehenden FileCrypt- oder Hide.cx-Ordnern anlegen und ihre Links behalten."
---

Bearcat liest die Ordner eines Linkcrypteraccounts und legt pro Releasename ein Release an.
Mirrors mit brauchbaren Links übernimmt Bearcat als abgeschlossene Uploads. Die übrigen Hoster des Releasetemplates lädt Bearcat selbst hoch.

## Unterstützte Linkcrypter

- **FileCrypt**: Der API-Schlüssel braucht das Recht `LIST`, um deine Ordner aufzulisten.
- **Hide.cx**

## Ablauf

1. Öffne **Crypter** > **Linkcrypterimporte** und klicke auf **Neuer Import**.
2. Wähle die Crypterregistrierung und das Releasetemplate. Das Template legt Hoster, Archiveinstellungen und Linkcryptereinstellungen der neuen Releases fest.
3. Optional: Wähle einen **Archivbasisordner für Releases ohne lokale Dateien**. Du brauchst ihn für Releases ohne lokalen Releaseordner: Bearcat stellt ihre Archive dort aus den importierten Mirrors wieder her.
4. Bearcat liest alle Ordner und ihre Links im Hintergrund. Ordner, deren URL Bearcat schon kennt, überspringt es.
5. Wähle pro Hoster einen Hosteraccount und klicke auf **Dateien prüfen**. Bearcat prüft die Dateien nur mit diesem Account.
6. Prüfe die Ordner, wähle die zu importierenden aus und klicke auf **Import starten**.

## So entscheidet Bearcat über Mirrors

Jeder Mirror wird einer Uploadkonfiguration mit demselben Hoster zugeordnet: bei einem neuen Release aus dem Template, bei einem bestehenden Release aus dem Release.

| Entscheidung | Wann |
| --- | --- |
| **Wird übernommen** | Alle Dateien sind online und sind Archivteile (`.rar`, `.r00`, `.7z`, `.7z.001`). Jeder weitere übernommene Mirror braucht dieselben Dateinamen wie der erste übernommene Mirror. |
| **Bearcat lädt diesen Hoster selbst hoch** | Der Hoster ist im Release, aber eine Bedingung oben ist nicht erfüllt. |
| **Nicht unterstützter Hoster** | Bearcat kennt den Hoster der Links nicht. |
| **Hoster nicht im Release** | Das Release hat keine weitere Uploadkonfiguration für diesen Hoster. |

Bei einem verwalteten Template sucht Bearcat in den Arbeitsverzeichnissen und den Basispfaden der
Ordnerautomatisierungen einen unbenutzten Ordner mit dem Releasenamen. Findet es einen, wird das Release mit
diesem Ordner verwaltet. Sonst wird es unverwaltet und Bearcat stellt sein Archiv bei Bedarf aus den Mirrors wieder her.

## Ordner, die sich nicht importieren lassen

- Kein Mirror gehört zu einem Hoster des Releases.
- Kein Mirror kann übernommen werden und es gibt keinen Releaseordner.
- Es gibt keinen Releaseordner und du hast keinen Archivbasisordner gewählt.

Die letzten beiden gelten nur für Ordner, die ein neues Release anlegen.

## Bestehende Releases

Der Ordnername ist der Releasename (Groß- und Kleinschreibung zählt nicht).

- Ordner mit demselben Releasenamen landen in einem Release. Ein Ordner pro Hoster funktioniert.
- Ein Ordner, dessen Release es schon gibt, wird an dieses Release angehängt, zum Beispiel der Ordner eines zweiten Crypters.
- Ein Mirror mit denselben Links wie ein bestehender Upload wird mit diesem Upload verknüpft.
- Ein Hoster, der schon einen neueren Upload hat, bekommt die aktuellen Links beim nächsten Containerupdate.
- Ein Ordner schlägt fehl, wenn das Release für einen seiner Hoster schon einen Container desselben Crypteraccounts hat.

## Linkcryptercontainer

- Ein Ordner mit einem Mirror eines Releasehosters wird ein Container dieser Uploadkonfiguration (**Jedes Release einzeln**).
- Ein Ordner mit mehreren Mirrors wird ein Container **Release (alle Hoster als Mirrors)**. Legt der Ordner das Release an, kommen Hoster, die im Ordner fehlen, nach ihrem Upload in diesen Container.

Siehe [Linkcryptercontainer](/Bearcat/de/upload-lifecycle/#5-linkcryptercontainer).

## Nach dem Import

- Neue Releases bekommen die **Primärsprache** des Imports. Bearcat nutzt sie für ihre Releaseinformationen. Bestehende Releases behalten ihre Sprache.
- Releaseinformationen (xrel, PreDB, NFO) ruft Bearcat beim Import nicht ab. Die Hintergrundaufgabe für Releaseinformationen holt sie danach ab.
- Importierte Releases gelten als gepostet und erscheinen nicht in der [Postwarteschlange](/Bearcat/de/post-queue/).
