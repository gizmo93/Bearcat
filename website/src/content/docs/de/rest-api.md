---
title: "REST-API verwenden"
description: "Releases, Archive und Uploads abfragen und Befehle über die REST-API von Bearcat ausführen."
---

Mit der REST-API können externe Tools Releases, Archive und Uploads lesen und Befehle an Bearcat senden.
Sie verwendet die Adresse und den Port der Weboberfläche.

## Endpunkte und Dokumentation

| Pfad | Zweck |
| --- | --- |
| `/api/v1` | API-Endpunkte |
| `/openapi/v1.json` | OpenAPI-Spezifikation |
| `/api/docs` | Interaktive Dokumentation |

Läuft Bearcat zum Beispiel unter `http://localhost:5000`, öffne `http://localhost:5000/api/docs`
oder lade die Spezifikation von `http://localhost:5000/openapi/v1.json` herunter.

Die [API-Referenz](/Bearcat/api/) listet ebenfalls die Endpunkte, Parameter und Schemas auf.

## Authentifizierung

Wer Bearcat erreichen kann, kann Daten über `GET`-Endpunkte lesen. Betreibe Bearcat in einem privaten
Netzwerk oder beschränke den Zugriff mit einem Reverse Proxy mit Basic Auth oder einem VPN.

Befehle (`POST`) brauchen einen API-Schlüssel im Header `X-Api-Key`.
Einrichtung und Beispiele findest du unter [Bearcat mit externen Tools steuern](/Bearcat/de/external-orchestration/).

## Client generieren

Mit [Kiota](https://learn.microsoft.com/openapi/kiota/) oder [NSwag](https://github.com/RicoSuter/NSwag)
kannst du einen typisierten Client generieren. Verwende dafür `/openapi/v1.json` deiner Bearcat-Installation.
