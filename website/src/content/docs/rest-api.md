---
title: "Use the REST API"
description: "Query releases, archives and uploads and run commands through the Bearcat REST API."
---

The REST API lets external tools read releases, archives, and uploads and send commands to Bearcat.
It uses the web interface’s address and port.

## Endpoints and documentation

| Path | Purpose |
| --- | --- |
| `/api/v1` | API endpoints |
| `/openapi/v1.json` | OpenAPI specification |
| `/api/docs` | Interactive documentation |

For example, on `http://localhost:5000`, open `http://localhost:5000/api/docs`
or download the specification from `http://localhost:5000/openapi/v1.json`.

The [API reference](/Bearcat/api/) also lists the endpoints, parameters, and schemas.

## Authentication

Anyone who can reach Bearcat can read data through `GET` endpoints. Keep it on a private network
or restrict access with a reverse proxy with basic auth or a VPN.

Commands (`POST`) require an API key in the `X-Api-Key` header.
See [Control Bearcat from External Tools](/Bearcat/external-orchestration/) for setup and examples.

## Generating a client

Generate a typed client with [Kiota](https://learn.microsoft.com/openapi/kiota/) or
[NSwag](https://github.com/RicoSuter/NSwag), using `/openapi/v1.json` from your Bearcat installation.
