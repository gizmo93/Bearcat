---
title: "Partgrösse ändern"
description: "Die Partgrösse vieler Archivkonfigurationen und Releasetemplates auf einmal ändern."
---

Öffne **Uploads und Archive > Archive** und klicke auf **Partgrösse ändern**. Wähle die aktuelle Partgrösse, gib
die neue ein und klicke auf **Partgrösse ändern**. Die Vorschau aktualisiert sich bei jeder Änderung.

Die Partgrösse richtet sich nach der globalen [Einheit der Partgrösse](/de/advanced-configuration/#einheit-der-partgrösse).

## Was sich ändert

- Jede Archivkonfiguration mit genau der gewählten Partgrösse erhält die neue Partgrösse.
- Mit **Auch Releasetemplates ändern** (Standard: an) ändern sich auch die Archivkonfigurationen in
  Releasetemplates, damit neue Releases die neue Partgrösse verwenden.

Nur Archive, die ab jetzt gepackt werden, verwenden die neue Partgrösse. Bestehende Archive und Uploads
behalten ihre Partgrösse, bis sie neu gepackt werden.

## Übersprungene Konfigurationen

Die neue Partgrösse muss bei allen Zielhostern eines Archivs erlaubt sein. Überschreitet sie das
Dateigrössenlimit eines Hosters, überspringt Bearcat diese Archivkonfiguration. Das gilt auch für Releasetemplates.

Die Vorschau listet jede übersprungene Konfiguration mit Hoster, Limit und einem Link zum Release oder
Releasetemplate.
