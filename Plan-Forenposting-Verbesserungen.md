# Plan: Forenposting verbessern

Stand 2026-11-17, nach Review durch Fable. Ziel: drei Lücken im automatischen Forenposting schliessen, mit möglichst wenig neuem Code. Keine neuen Abstraktionsschichten, keine Konfiguration, wo feste Werte reichen.

Grundlage: Vergleich mit IntelligeN und Messungen auf data-load.me und boerse.cx (nur Suchen).

## Messergebnisse, die den Plan bestimmen

- Beide Foren: keine Flood-Sperre bei der Suche (rund 10 Suchen pro Minute ohne Sperre).
- Beide Foren: "keine Treffer" ist HTTP 200 mit einer forenspezifischen Meldung. Im HTML nicht von einer Fehlerseite zu unterscheiden. Der Such-POST mit `_xfResponseType=json` liefert auf beiden Foren `status: ok` plus `redirect`, auch ohne Treffer. Fehler kämen als `status: error`.
- boerse.cx: Präfix-Labels stehen im Titellink (`<a><span class="label">1080p/x265</span> Joker 2019 ...</a>`). `TextContent` liefert den Präfix mit.
- Posting-Flood ist nicht gemessen (bräuchte echte Posts).

## Phase 1: Hoster-, Crypter- und Bildhosterlisten pro Forum

**Entscheidungen des Users:** Erforderlich heisst "mindestens einer". Verboten heisst "aus dem Post filtern; blockieren nur, wenn von dieser Art danach nichts mehr übrig ist". Gilt auch für Bildhoster.

**Daten:** Sechs neue Properties auf `DistributionSiteRegistration`, als `List<string>` (Primitive Collection, wird schon bei `Upload.ErrorMessages` auf beiden Providern genutzt):

- `RequiredHosterClassNames`, `ForbiddenHosterClassNames`
- `RequiredLinkCrypterClassNames`, `ForbiddenLinkCrypterClassNames`
- `RequiredImageHosterClassNames`, `ForbiddenImageHosterClassNames`

Gespeichert werden Klassennamen (Typ), nicht Accounts. Migration für beide Provider, Default leere Liste.

**Read Models:** Die Klassennamen gehen heute beim Bauen der Render-Daten verloren (`ReleaseForumPostUploadBuilder.cs:50-81`, `ForumPostImageLinkBuilder.cs:139`). `ReleaseForumPostUploadReadModel`, `ReleaseForumPostLinkCrypterReadModel` und das Image-Link-Read-Model bekommen je den Klassennamen. Im Template bleibt er unsichtbar.

**Logik:** Statische Klasse `ForumServiceRestrictions` in `Domain/Shared/ForumPosting` mit einer generischen Methode, die pro Art einmal aufgerufen wird (also dreimal):

`Apply<T>(IReadOnlyList<T> items, Func<T, string> classNameOf, IReadOnlyList<string> required, IReadOnlyList<string> forbidden, string kindLabel)` gibt die gefilterte Liste und einen optionalen Blockiergrund zurück.

- Verbotene entfernen.
- Vorher mindestens ein Eintrag, danach keiner: Blockiergrund "kein erlaubter {kindLabel}".
- Erforderlich-Liste nicht leer und kein verbleibender Eintrag darin: Blockiergrund "einer von X, Y fehlt".

Bewertet wird pro Release bzw. pro Collection als Ganzes (alle Uploads, Slots und Mitglieder zusammen), nicht pro Mitglied.

**Wo gefiltert wird:** Nicht im `ForumPostRenderService` (der sieht nur das fertige `ScriptObject`), sondern beim Bauen der Render-Daten. `IForumPostRenderSource.BuildGlobalsAsync` bekommt einen optionalen Parameter `distributionSiteRegistrationId`. Ist er gesetzt, laden die Render Sources die Listen und filtern, bevor sie die Globals bauen. Stellen:

- Release: Uploads, Upload-Crypter, All-Hosters-Crypter, `imagelinks` (`ReleaseForumPostRenderModelBuilder`).
- Collection: Uploads und Crypter der Mitglieder (`ReleaseCollectionForumPostRenderSource.cs:184-185`), Slot-Crypter (`:120`, `:197`), `imagelinks` der Mitglieder (`:89`) und der Collection (`:123`).

`RenderAsync` reicht den Parameter durch und gibt die Blockiergründe im `ForumPostTemplateRenderResult` zurück (neues Feld `BlockedReasons`).

**Container mit mehreren Hostern** (All-Hosters, Collection-Slots): werden nur nach der Crypter-Liste gefiltert. Ein verbotener Hoster darin wird ignoriert, weil er sich aus dem Container nicht entfernen lässt. Das kommt in die Doku.

**Bildhoster:** Templates sprechen Bilder über den Config-Namen an (`imagelinks.<config>.full`). Ein gefilterter Bildhoster rendert dort leer, also z.B. `[img][/img]`. Das blockiert nicht und ist bewusst so: Wer Bildhoster-Vorgaben nutzt, prüft im Template mit `if` oder nutzt pro Forum ein eigenes Template. Kommt in die Doku.

**Aufrufer, die die Registration mitgeben müssen:**
- `AutoForumPostCreationService` und `AutoForumPostUpdateService` (inklusive `UpdatePostedLocationAsync`, `AutoForumPostUpdateService.cs:81`, genutzt von `PostedLocations.razor.cs:148`).
- Manueller Wizard (`PostToForumDialog.razor.cs:290-293`): zeigt Blockiergründe als Warnung, Posten bleibt möglich.
- `RenderForumPostDialog.razor.cs:77`: bleibt ohne Forum (reine Vorschau).

**Ergebnis im Automatikpfad:** Blockiergründe heisst `Skipped` mit neuem `AutoPostSkipReason.ServiceRestrictionNotMet`. Keine Benachrichtigung, kein Fehlversuch. Wichtig: Die Services melden heute jedes Nicht-Posted-Ergebnis als "fehlgeschlagen" (`AutoForumPostCreationService.cs:66`, `AutoForumPostUpdateService.cs:108`). Für `Skipped` darf keine Fehler-Benachrichtigung entstehen.

**Kosten akzeptiert:** Ein dauerhaft blockiertes Release bleibt in der Queue und wird bei jedem Lauf (15 min) einmal pro Forum gerendert. Das betrifft nur noch nicht gepostete Releases. Eine billige Vorprüfung im Plan-Service lassen wir weg, solange das kein messbares Problem ist.

**UI:** Auf der Postingregel-Seite des Forums (dort liegen schon `EnableAutomaticPosting` und `StripDotsForThreadSearch`) sechs Mehrfachauswahlen aus den bekannten Hoster-, Crypter- und Bildhoster-Typen.

**Tests:** Unit-Tests für `ForumServiceRestrictions.Apply` (filtern, blockieren, erforderlich). Sonst keine.

## Phase 2: Fehlversuche, Backoff, Postingabstand

**Problem heute:** Ein fehlgeschlagener Post wird alle 15 Minuten endlos wiederholt, jedes Mal mit neuer Benachrichtigung. Zwischen Posts gibt es keine Pause.

**Daten:** Neue Entity `ForumPostingFailure`:

- `Id`, `ReleaseId?`, `ReleaseCollectionId?`, `DistributionSiteRegistrationId`
- `FailedAttemptCount`, `NextAttemptAt`, `LastErrorMessage`
- `State` (Enum ab 1): `RetryScheduled = 1`, `WaitingForThreadSelection = 2`, `GaveUp = 3`

Ein Datensatz pro Release/Collection und Forum: zwei gefilterte Unique-Indizes (`ReleaseId, DistributionSiteRegistrationId` und `ReleaseCollectionId, DistributionSiteRegistrationId`) auf beiden Providern, Cascade Delete wie bei `PostedLocationConfiguration`. Erstellen und Aktualisieren teilen den Datensatz, weil für dieselbe Kombination nie beides gleichzeitig ansteht.

**Benachrichtigungen aus den Services in den Runner verschieben:**
- `AutoForumPostCreationService` und `AutoForumPostUpdateService` erzeugen keine Fehler-Benachrichtigungen mehr. Die Erfolgs-Benachrichtigung ("automatisch gepostet") bleibt nur im Runner.
- Der Dialog zeigt das Ergebnis direkt an, wie heute schon.
- Der Runner benachrichtigt nur beim Übergang nach `GaveUp` oder `WaitingForThreadSelection`. Normale Fehlschläge nur ins Log.

**Ablauf im Runner:**
- Vor dem Posten oder Aktualisieren: Gibt es einen Datensatz mit `GaveUp` oder `WaitingForThreadSelection` oder `NextAttemptAt` in der Zukunft, wird die Seite übersprungen.
- Erfolg: Datensatz löschen.
- Fehlschlag: Zähler erhöhen, `NextAttemptAt` = jetzt + feste Stufe (15 min, 1 h, 4 h, 12 h). Nach dem 5. Fehlschlag `GaveUp`.

**Postingabstand:** Neue Property `MinimumSecondsBetweenPosts` auf `DistributionSiteRegistration` (Default 30). Der Runner bleibt sequenziell und merkt sich in einem Dictionary den letzten Post pro Forum; vor dem nächsten Post ins selbe Forum wartet er die Restzeit mit `Task.Delay`. Gilt für Erstellen und Aktualisieren, nur im Runner, nicht im Dialog. Kein Parallelisieren über Foren.

**UI:** Der Plan-Service lädt die Datensätze mit, `AutoPostSiteEntry` bekommt `FailureState`, `FailedAttemptCount` und `LastErrorMessage`. Der Auto-Post-Abschnitt zeigt bei `GaveUp` die letzte Fehlermeldung und "Erneut versuchen" (löscht den Datensatz). Das gilt für ausstehende Posts und für ausstehende Aktualisierungen. Feld für den Abstand auf der Postingregel-Seite.

**Bewusst weggelassen:** Keine Fehlerklassen (Login, Cloudflare, Flood). Alles geht durch denselben Backoff. Keine Prüfung "wurde trotz Timeout schon gepostet". Kein Auslesen von Flood-Wartezeiten.

**Tests:** Keine, ausser die Abfrage der Datensätze wird mehr als ein einfaches `Where`.

## Phase 3: Threadsuche und Threadabgleich

**Problem heute:** `AutoForumPostCreationService` antwortet ohne Titelprüfung auf `existingThreads[0]` (ältester Treffer). Funktioniert, solange die Suche genau einen Treffer liefert.

**Suche im Client (`XenForoForumClient.SearchThreadsAsync`):**
- Such-POST mit `_xfResponseType=json`:
  - `status: error`: Exception mit den Meldungen (läuft dann durch den Backoff aus Phase 2).
  - `status: ok` mit `redirect`: Ergebnisseite per GET laden und wie bisher parsen.
  - `status: ok` ohne `redirect`: keine Treffer (nicht gemessen, aber bei Standard-XenForo möglich).
  - Die bestehende `ReadRedirectUrlFromJsonResponse` passt nicht direkt (wirft ohne Redirect, Text "rejected the post"). Eine eigene kleine Auswertung für die Suche, die `ReadErrorMessages` wiederverwendet.
- Vor dem Lesen des Titels `.label` und `.label-append` aus dem Link entfernen.
- Die Sortierung nach Thread-ID fällt weg.
- Zweiter Aufrufer `FindNewThreadUrlAsync` (`XenForoForumClient.cs:632`, läuft nach einem erfolgreichen neuen Thread): Ein Suchfehler dort wird abgefangen und als "nicht gefunden" behandelt. Sonst würde ein erfolgreicher Post als Fehlschlag gezählt und beim nächsten Versuch doppelt gepostet.

**Abgleich:** Statische Klasse `ForumThreadTitleMatcher` in `Domain/Shared/ForumPosting` mit `FindMatchingThreads(string searchName, IReadOnlyList<ExistingThread> threads)`:

1. Beide Seiten normalisieren: Kleinbuchstaben, alles ausser Buchstaben und Ziffern wird zu Leerzeichen, Mehrfach-Leerzeichen zusammenfassen.
2. Gibt es Threads mit exakt gleichem normalisiertem Titel, nur diese zurückgeben.
3. Sonst alle Threads, deren Titel alle Wörter des Suchnamens als ganze Wörter in derselben Reihenfolge enthält.

**Auswertung in `AutoForumPostCreationService`:**
- 0 Treffer: neuer Thread.
- 1 Treffer: Antwort in diesen Thread.
- mehrere: kein Post; neues Ergebnis "Auswahl nötig", das der Runner als `WaitingForThreadSelection` speichert (mit Benachrichtigung).

**Auswahl im Dialog:** Im Auto-Post-Abschnitt zeigt ein Forum mit `WaitingForThreadSelection` den Button "Thread wählen". Er sucht live erneut und zeigt die passenden Threads plus "Neuer Thread". Bestätigen ruft eine neue Methode `CreatePostWithChosenThreadAsync(plan, site, string? threadUrl)` auf (`null` = neuer Thread). Sie teilt sich Rendern, Absenden und Speichern mit dem automatischen Pfad, nur die Suche entfällt. Unterforum, Präfix und Template kommen aus der Regel. Erfolg löscht den Datensatz.

**Manueller Wizard:** Der Thread-Schritt wählt per Matcher vor (genau ein Treffer: dieser; sonst "Neuer Thread") statt des ersten Treffers.

**Tests:** Unit-Tests für `ForumThreadTitleMatcher` (exakt vor Wörtern, REPACK-Fall wird mehrdeutig, kein Treffer). Ein Client-Test mit gemocktem HTTP für die JSON-Auswertung der Suche (Fehler, ok ohne Redirect).

## Reihenfolge und Review

Drei Phasen, nach jeder Phase Stopp für Review. Doku auf Englisch und Deutsch pro Phase ergänzen.
