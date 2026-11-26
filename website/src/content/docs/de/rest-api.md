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

Bei der Desktopanwendung und dem Windows-Dienst öffnest du normalerweise
`http://127.0.0.1:17208/api/docs`, bei Docker `http://localhost:8080/api/docs`.
Passe Adresse und Port an, wenn Bearcat auf einem anderen Rechner oder Port läuft.

Die [API-Referenz](/api/) listet ebenfalls die Endpunkte, Parameter und Schemas auf.

## Authentifizierung

Wer Bearcat erreicht, kann Daten über `GET`-Endpunkte lesen. Betreibe Bearcat in einem privaten
Netzwerk oder beschränke den Zugriff über ein VPN oder einen Reverse Proxy mit Basic Auth.

Befehle (`POST`) brauchen einen API-Schlüssel im Header `X-Api-Key`.
Einrichtung und Beispiele findest du unter [Bearcat mit externen Tools steuern](/de/external-orchestration/).

## Client generieren

Mit [Kiota](https://learn.microsoft.com/openapi/kiota/) oder [NSwag](https://github.com/RicoSuter/NSwag)
kannst du einen typisierten Client generieren. Verwende dafür `/openapi/v1.json` deiner Bearcat-Installation.
