---
title: "Use the REST API"
description: "Query releases, archives and uploads through the Bearcat REST API."
---

Bearcat offers a (currently) read-only REST API to query your releases, archives
and uploads. The API is part of the running Bearcat host, so it is available on the same address and
port as the web UI.

## Endpoints and documentation

- The API itself lives under `/api/v1`.
- The OpenAPI document is served at `/openapi/v1.json`.
- Interactive documentation is available at `/api/docs` on the running host.

If Bearcat runs on `http://localhost:5000`, the interactive documentation is at
`http://localhost:5000/api/docs` and the spec at `http://localhost:5000/openapi/v1.json`.

To see all endpoints with their parameters and schemas, you can also use the browsable reference at
[https://gizmo93.github.io/Bearcat/api/](https://gizmo93.github.io/Bearcat/api/).

## Authentication

Currently no authentication. Bearcat is meant to run inside a private network, and the API is open to anyone who
can reach the host. Do not expose the Bearcat host to the internet without putting your own
authentication in front of it (for example a reverse proxy with basic auth or a VPN).

## Generating a client

The Bearcat API follows the Open API standard, so you can just use the available openapi.json, to generate a client for the programming language you are using.
Point a code generator such as [Kiota](https://learn.microsoft.com/openapi/kiota/) or
[NSwag](https://github.com/RicoSuter/NSwag) at `/openapi/v1.json` and let it create a typed client for you.
