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

For the Desktop app and Windows service, open `http://127.0.0.1:17208/api/docs` by default.
For Docker, open `http://localhost:8080/api/docs`. Adjust the address and port if Bearcat
runs on another computer or port.

The [API reference](/api/) also lists the endpoints, parameters, and schemas.

## Authentication

Anyone who can reach Bearcat can read data through `GET` endpoints. Keep it on a private network
or restrict access with a reverse proxy with basic auth or a VPN.

Commands (`POST`) require an API key in the `X-Api-Key` header.
See [Control Bearcat from External Tools](/external-orchestration/) for setup and examples.

## Generating a client

Generate a typed client with [Kiota](https://learn.microsoft.com/openapi/kiota/) or
[NSwag](https://github.com/RicoSuter/NSwag), using `/openapi/v1.json` from your Bearcat installation.
