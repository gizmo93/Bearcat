---
title: "Use the REST API"
description: "Query releases, archives and uploads and run commands through the Bearcat REST API."
---

Use Bearcat's REST API to query releases, archives, and uploads, and to run a few commands.
It uses the same address and port as the web interface.

## Endpoints and documentation

- The API itself lives under `/api/v1`.
- The OpenAPI document is served at `/openapi/v1.json`.
- Interactive documentation is available at `/api/docs` on the running host.

If Bearcat runs on `http://localhost:5000`, the interactive documentation is at
`http://localhost:5000/api/docs` and the spec at `http://localhost:5000/openapi/v1.json`.

The [API reference](/Bearcat/api/) also lists the endpoints, parameters, and schemas.

## Authentication

Read endpoints (`GET`) have no authentication. Anyone who can reach the Bearcat host can access them.
Keep Bearcat on a private network, or add your own
authentication in front of it (for example a reverse proxy with basic auth or a VPN).

Commands (`POST`) are disabled until you configure an API key and must send it in the `X-Api-Key` header.
See [Orchestrate Bearcat from External Tools](/Bearcat/external-orchestration/) for the setup and an example workflow.

## Generating a client

Point an OpenAPI code generator such as [Kiota](https://learn.microsoft.com/openapi/kiota/) or
[NSwag](https://github.com/RicoSuter/NSwag) at your host's `/openapi/v1.json` to generate a typed client.
