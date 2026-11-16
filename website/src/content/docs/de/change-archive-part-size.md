---
title: "Partgrösse ändern"
description: "Die Partgrösse vieler Archivkonfigurationen und Releasetemplates auf einmal ändern."
---

Öffne **Uploads und Archive > Archive** und klicke auf **Partgrösse ändern**. Wähle die aktuelle Partgrösse, gib
die neue ein und klicke auf **Partgrösse ändern**. Die Vorschau aktualisiert sich bei jeder Änderung.

Die Partgrösse richtet sich nach der globalen [Einheit der Partgrösse](/Bearcat/de/advanced-configuration/#einheit-der-partgrösse).

## Was sich ändert

- Jede Archivkonfiguration mit genau der gewählten Partgrösse erhält die neue Partgrösse.
- Mit **Auch Releasetemplates ändern** (Standard: an) ändern sich auch die Archivkonfigurationen in
  Releasetemplates, damit neue Releases die neue Partgrösse verwenden.

Nur Archive, die ab jetzt gepackt werden, verwenden die neue Partgrösse. Bestehende Archive und Uploads
behalten ihre Partgrösse, bis sie neu gepackt werden.

## Übersprungene Konfigurationen

Zielt eine Uploadkonfiguration einer Archivkonfiguration auf einen Hoster, dessen Dateigrössenlimit unter
der neuen Partgrösse liegt, bleibt die Archivkonfiguration unverändert. Dasselbe gilt für Releasetemplates.

Die Vorschau listet jede übersprungene Konfiguration mit Hoster, Limit und einem Link zum Release oder
Releasetemplate.
