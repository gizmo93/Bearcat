# IntelliGen und Bearcat: Automatisches Forenposting

Chat vom 17. November 2026. Enthält die Nutzerfragen und sichtbaren Antworten dieses Threads bis zur Exportanfrage, ohne interne Anweisungen und Werkzeugprotokolle. Der Inhalt des verlinkten früheren Chats ist nicht enthalten, da er nicht abgerufen werden konnte.

---

## Nutzer

[https://chatgpt.com/share/6afc851f-50f0-83eb-a104-a98e5ee0ad05](https://chatgpt.com/share/6afc851f-50f0-83eb-a104-a98e5ee0ad05)

Ich habe das Intelligen repo hier:
/Users/gizmo\_/dev/IntelligeN/res

mal ausgecheckt.

Kannst du schauen, welche Features Bearcat beim Automatischen Forenposting dagegen misst?
Abgesehen davon, dass wir aktuell nur XenForo haben. Solche Plugins sind ja schnell implementiert.

Ich wüsste gerne, ohne, dass ich diesen Delphi Code kompilieren muss, was IntelliGen konnte und wie es funktioniert hat, auch aus User Perspektive. Was ist gemäss Code der Workflow der User?

## Assistent – Zwischenstand

Ich schaue mir den IntelliGen-Code und Bearcats Forenposting an. Daraus rekonstruiere ich den Nutzerworkflow und vergleiche die Funktionen unabhängig von den unterstützten Forensystemen.

## Assistent – Zwischenstand

Ein wichtiger Unterschied zeichnet sich ab: IntelliGen war ein Desktopeditor mit Sammelveröffentlichung, Vorlagen und Regeln pro Zielseite. Beim Ordnerimport muss man genauer hinsehen: Es gibt Optionen für eine automatische Verarbeitung, aber der bisher gelesene DirWatch-Code öffnet neue Dateien lediglich als Tabs. Ich prüfe, welche Funktionen tatsächlich ausgeführt werden und welche nur als Einstellungen vorbereitet sind.

## Assistent – Zwischenstand

Die grössten belegten Unterschiede liegen bisher bei frei gestaltbaren Threadtiteln, Hostervorgaben pro Forum und der Steuerung von Sammelaufträgen. Bearcat kann dafür bestehende Posts nach Reuploads aktualisieren; IntelliGens `EditArticle` ist im Checkout nur ein TODO. Auch der Button "AutoPublish" ist aufschlussreich: Er veröffentlicht die aktuell geöffneten Tabs als Sammelauftrag.

## Assistent – Analyse

**Bearcat deckt den Kern bereits ab und geht bei der dauerhaften Automatisierung teilweise weiter.** IntelliGen hatte dafür mehr Möglichkeiten, Posts je Forum anzupassen und grössere Veröffentlichungsaufträge interaktiv zu steuern.

Ich habe den lokalen IntelliGen-Checkout und Bearcats aktuellen Code verglichen, ohne etwas zu kompilieren oder zu ändern. Das beschreibt die implementierten Abläufe; ob alte Forenadapter heute noch funktionieren, ist damit nicht geprüft. Den geteilten Chat konnte ich nicht abrufen.

**So sah der Nutzerworkflow in IntelliGen aus:**

1. **Zielseiten einmalig einrichten.**  
   Über einen Assistenten eine Website hinzufügen, das Forensystem erkennen lassen oder auswählen und Zugangsdaten hinterlegen. Pro Zielseite wurden Titelvorlage, Beitragsvorlage und Foreneinstellungen konfiguriert.

   Die Zuordnung zu Unterforen konnte vom Inhalt abhängen: zunächst beispielsweise `Movie`, darunter weitere Bedingungen anhand von Datenfeldern. Entsprechend konnten auch Thread-IDs und Präfixe zugeordnet werden. Damit war etwa möglich: Filme grundsätzlich ins Filmforum, bestimmte Varianten in ein anderes Unterforum oder in einen festen Sammelthread.

2. **Ein Release als Tab öffnen oder neu anlegen.**  
   Ein Tab enthielt einen Datensatz. Typvorlagen wie Movie, Audio, Software oder EBook bestimmten dessen Eingabefelder. Dazu gehörten beispielsweise Releasename, Titel, Beschreibung, Sprache, Cover, NFO und Passwort.

   XML war ein Import- und Speicherformat für diese Daten. Man konnte mehrere Dateien gleichzeitig öffnen. **XML war keine Voraussetzung dafür, IntelliGen zu benutzen.**

3. **Daten und Links vorbereiten.**  
   Der Nutzer konnte Felder bearbeiten und über **AutoCompletion** Crawler starten, die Metadaten ergänzten. Dazu kamen Funktionen zum Prüfen von Downloadlinks, Erstellen und Prüfen von Linkcryptercontainern sowie Hochladen von Bildern.

   Diese Schritte waren einzeln und teilweise als Sammelaktionen zugänglich. Sie waren im normalen Veröffentlichungsablauf nicht automatisch alle vorgeschaltet.

4. **Den Beitrag für die jeweilige Website prüfen.**  
   IntelliGen hatte Daten-, Code-/Design- und Vorschauansichten. Titel und Beitrag entstanden aus getrennten IScript-Vorlagen mit Variablen, Bedingungen und Schleifen.

   Dieselben Releasedaten konnten deshalb je Website anders aussehen: anderer Titel, andere Formatierung, andere Hosterreihenfolge und ein anderer Bildhoster.

5. **Veröffentlichen auslösen.**  
   Es gab drei relevante Varianten:
   - Nur auf einer ausgewählten Zielseite veröffentlichen.
   - Den aktuellen Tab auf allen dafür aktiven Zielseiten veröffentlichen.
   - Mit **AutoPublish** alle geöffneten Tabs als Sammelauftrag veröffentlichen.

   **Der Name „AutoPublish“ bedeutete hier konkret Sammelveröffentlichung.** Der Button ruft `CallSeriesPublish` auf, sammelt die offenen Tabs und stellt einen Auftrag in die Warteschlange.

6. **Bei Bedarf während des Postens eingreifen.**  
   Mit aktiviertem „Intelligent Posting“ suchte das Plugin nach vorhandenen Threads. Bei einem passenden Treffer wurde geantwortet, ansonsten ein Thread erstellt.

   Optional erschien ein Dialog mit den Suchresultaten. Dort konnte der Nutzer einen Thread wählen, „Create new Thread“ wählen, den Suchtext ändern und erneut suchen oder abbrechen. Unterstützte Plugins konnten auch CAPTCHA-Eingaben anfordern.

7. **Fortschritt und Fehler in der Publish Queue verfolgen.**  
   Die Oberfläche zeigte Aufträge, Fortschritt und Fehlermeldungen. Aufträge liessen sich pausieren, fortsetzen und abbrechen. Einstellbar waren parallele Veröffentlichungen, eine Pause zwischen Releases innerhalb eines Sammelauftrags und Wiederholungen bei Fehlern.

Die zentralen Einstiegspunkte dafür sind [ufMain.pas](sandbox:/Users/gizmo_/dev/IntelligeN/src/view/frames/ufMain.pas), der [Website-Einstellungsparser](sandbox:/Users/gizmo_/dev/IntelligeN/src/sdk/plugins/cms/uPlugInCMSSettingsHelper.pas) und die [Publish Queue](sandbox:/Users/gizmo_/dev/IntelligeN/src/view/frames/ufPublishQueue.pas).

**Das eigentliche Forenposting lief über HTTP-Anfragen.** IntelliGen meldete sich an, übernahm Cookies und Sicherheitstokens, lud bei Bedarf das Beitragsformular und schickte dessen Felder ab. Die Antwort wurde auf Erfolg oder Fehlermeldungen geprüft. Für den normalen Postingvorgang musste der Nutzer keinen Browser bedienen. Das lässt sich in [uPlugInCMSClass.pas](sandbox:/Users/gizmo_/dev/IntelligeN/src/sdk/plugins/cms/uPlugInCMSClass.pas) nachvollziehen.

Beim Funktionsvergleich ergeben sich diese Unterschiede:

| Funktion | IntelliGen im Checkout | Bearcat aktuell |
|---|---|---|
| Unterforum und Präfix anhand von Bedingungen wählen | Kategoriezuordnung mit verschachtelten Feldvergleichen | Bereits vorhanden; verschachtelte UND/ODER/NICHT-Bedingungen, Regex, Zahlenvergleiche und Regelvorschau |
| Individuelle Beitragsvorlagen | IScript mit Bedingungen und Schleifen | Bereits vorhanden mit Scriban und Livevorschau |
| **Individuelle Threadtitel** | Separate Titelvorlage je Website | **Fehlt beim automatischen Posten.** Verwendet Release-/Collectionname, optional mit ersetzten Punkten |
| **Hostervorgaben pro Forum** | Dateihoster ausschliessen und priorisieren; Bildhoster priorisieren | **Keine entsprechende Foreneinstellung.** Auswahl lässt sich teilweise in der Beitragsvorlage ausdrücken |
| Suche nach bestehenden Threads | Forensuche plus eigener Titelabgleich; je Plugin zusätzliche Suchoptionen | Forensuche vorhanden; automatisches Posten nimmt den ersten zurückgegebenen Treffer |
| **Immer in einen bestimmten Thread antworten** | Über konfigurierte Thread-ID möglich | **Kein eigener Modus in Postingregeln** |
| Treffer vor dem Absenden auswählen | Optionaler Dialog direkt während des Postingauftrags | Im manuellen Postingdialog möglich; kein solcher Zwischenstopp im Hintergrundlauf |
| **Sammelaufträge steuern** | Parallelität, Pause zwischen Releases, Retryanzahl, Pause/Fortsetzen/Abbruch | Hintergrundlauf verarbeitet nacheinander; keine vergleichbare Postingauftragssteuerung |
| CAPTCHA beim Forenposting | Interaktiver Eingabedialog bei unterstützten Plugins | Im aktuellen XenForo-Ablauf nicht unterstützt |
| Zusätzliche Forenoptionen | Je Plugin etwa Tags, Signatur, Thema schliessen oder anheften | Keine entsprechenden allgemeinen Postingoptionen |
| **Bestehende Posts nach Reuploads aktualisieren** | Kein durchgängiger implementierter Ablauf gefunden | **Bereits vorhanden**, auch für Collections |
| **Dauerhafte Zuordnung Release → veröffentlichter Post** | Keine vergleichbare durchgängige Verwaltung im Postingpfad gefunden | **Vorhanden**, inklusive URL, Vorlage und Aktualisierungsstand |
| **Unbeaufsichtigtes Posten mit Qualitätsprüfung** | Über externe Steuerung möglich; integrierter Ordnerablauf unvollständig | **Vorhanden:** Hintergrundaufgabe, Qualitätsprüfung, Klassifizierung und Postingregeln |

Ein paar Unterschiede verdienen mehr Gewicht als die reine Tabelle.

**Die Threadsuche würde ich in Bearcat zuerst anschauen.** IntelliGens XenForo-Plugin normalisiert den Titel und prüft die Suchresultate nochmals mit einem daraus gebauten Muster. Das ist kein perfekter Abgleich, aber immerhin ein zusätzlicher Schritt.

Bearcats `FindExistingThreadsAsync` reicht die Suchresultate weiter. Der Client sortiert sie nach Thread-ID; `AutoForumPostCreationService` antwortet auf `existingThreads[0]`. Im automatischen Erstellungspfad findet danach keine eigene Prüfung statt, ob der Titel tatsächlich ausreichend zum Release passt.

Das bedeutet: Wenn die Forensuche ähnliche Treffer liefert, kann Bearcat den ältesten zurückgegebenen Thread wählen. **Eine explizite Trefferbewertung und ein definiertes Verhalten bei Mehrdeutigkeit wären hier sinnvoll.**

Belege: IntelliGens [XenForo-Suche](sandbox:/Users/gizmo_/dev/IntelligeN/src/sdk/plugins/cms/XenForo/uXenForo.pas), Bearcats [Suchclient](sandbox:/Users/gizmo_/RiderProjects/Bearcat/src/Bearcat.DistributionSites/Shared/XenForo/Api/XenForoForumClient.cs) und [Posterstellung](sandbox:/Users/gizmo_/RiderProjects/Bearcat/src/Bearcat.Domain/UseCases/PostToForums/PostCreation/AutoForumPostCreationService.cs).

**Hostervorgaben waren mehr als eine Formatierungsoption.** IntelliGen konnte beispielsweise für Forum A einen Dateihoster ausblenden und einen bestimmten Bildhoster bevorzugen. Diese Auswahl wurde vor dem Rendern auf die Daten angewendet. Wenn kein erlaubter Dateihoster vorhanden war, konnte die Zielseite für diesen Datensatz inaktiv werden.

Bearcat kann Links in Scriban filtern. Das ersetzt aber noch keine Einstellung wie „In diesem Forum nur posten, wenn mindestens einer dieser Hoster verfügbar ist“. Die relevante Implementierung liegt in [uApiPublishController.pas](sandbox:/Users/gizmo_/dev/IntelligeN/src/view/api/uApiPublishController.pas), insbesondere `HasAtLeastOneHosterAllowed` und `HandleBlackWhitelist`.

**Bei Postaktualisierungen ist Bearcat weiter.** IntelliGens gemeinsame Methode `EditArticle()` enthält ausdrücklich `TODO` und liefert `False`. Einzelne Plugins enthalten zwar vorbereitete Editierpfade, etwa phpBB3 anhand einer `ArticleID`. Der normale Publish-Aufrufer übergibt diese ID aber nicht; dort steht lediglich ein Kommentar `// ArticleID`.

Ich würde daher aus diesem Checkout **kein fertiges Feature „vorhandene Veröffentlichungen verwalten und aktualisieren“** ableiten. Bearcat hat dafür einen vollständigen [Aktualisierungsablauf](sandbox:/Users/gizmo_/RiderProjects/Bearcat/src/Bearcat.Domain/UseCases/PostToForums/PostUpdate/AutoForumPostUpdateService.cs).

**Zur Ordnerüberwachung lässt sich jetzt eine klare Aussage machen:** Das enthaltene DirWatch-Plugin reagiert auf Windows-Dateisystemereignisse. Bei einer neu hinzugefügten Datei ruft es `OpenToNewTab(...)` auf. Es pollt also nicht einfach regelmässig XML-Dateien durch.

Die Einstellungen enthalten zwar Optionen wie:

- vorhandene Dateien beim Start laden,
- Dateien nur einmal laden,
- nur IntelliGen-XML laden,
- Crawler und Crypter ausführen,
- speichern und veröffentlichen,
- Tab nach Veröffentlichung schliessen.

**Diese Optionen werden im vorliegenden DirWatch-Ausführungscode jedoch nicht umgesetzt.** Dort werden Pfad und Unterordnerüberwachung verwendet; neue Dateien werden geöffnet. Aus den Checkboxen allein darf man keinen fertigen automatischen Import- und Postingworkflow ableiten. Siehe [uDirWatchEngine.pas](sandbox:/Users/gizmo_/dev/IntelligeN/src/sdk/plugins/app/DirWatch/uDirWatchEngine.pas).

Echte externe Automatisierung war dagegen über **OLE/COM** vorgesehen und implementiert: Dateien öffnen, Crawler starten, Bilder hochladen, Links prüfen, Crypter starten, veröffentlichen, speichern und Tabs schliessen. Ein anderes Windowsprogramm konnte diese Schritte orchestrieren. Das war weiterhin die Steuerung der Desktopanwendung. Siehe [uOLE.pas](sandbox:/Users/gizmo_/dev/IntelligeN/src/view/ole/uOLE.pas).

Für Bearcat würde ich daraus diese Reihenfolge ableiten:

1. **Threadtreffer zuverlässig prüfen**, bevor automatisch geantwortet wird.
2. **Titelvorlagen ergänzen**, idealerweise Suchtext und Veröffentlichungstitel getrennt konfigurierbar.
3. **Hostervorgaben pro Forum ergänzen**, einschliesslich „ohne erlaubte Links nicht posten“.
4. **Postingpausen und gezielte Wiederholungen ergänzen.** IntelliGens globale Pause zwischen Releases ist dafür ein Anhaltspunkt; pro Forum wäre die Einstellung präziser.
5. **Feste Zielthreads unterstützen**, wenn Sammelthreads ein tatsächlicher Anwendungsfall sind.

Einen IntelliGen-Export würde ich vor allem als Kompatibilitätsfunktion für bestehende Nutzer sehen. Für den eigentlichen automatischen Veröffentlichungsworkflow hat Bearcat bereits die stärkere Grundlage: gespeicherte Veröffentlichungsorte, Qualitätsprüfung, Collections und Aktualisierungen nach Reuploads.

---
