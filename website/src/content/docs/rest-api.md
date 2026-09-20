---
title: "Use the REST API"
description: "Query releases, archives and uploads through the Bearcat REST API."
---

Use Bearcat's read-only REST API to query releases, archives, and uploads.
It uses the same address and port as the web interface.

## Endpoints and documentation

- The API itself lives under `/api/v1`.
- The OpenAPI document is served at `/openapi/v1.json`.
- Interactive documentation is available at `/api/docs` on the running host.

If Bearcat runs on `http://localhost:5000`, the interactive documentation is at
`http://localhost:5000/api/docs` and the spec at `http://localhost:5000/openapi/v1.json`.

The [API reference](/Bearcat/api/) also lists the endpoints, parameters, and schemas.

## Authentication

The API has no authentication. Anyone who can reach the Bearcat host can access it.
Keep Bearcat on a private network, or add your own
authentication in front of it (for example a reverse proxy with basic auth or a VPN).

## Generating a client

Point an OpenAPI code generator such as [Kiota](https://learn.microsoft.com/openapi/kiota/) or
[NSwag](https://github.com/RicoSuter/NSwag) at your host's `/openapi/v1.json` to generate a typed client.
