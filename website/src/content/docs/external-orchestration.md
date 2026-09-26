---
title: "Orchestrate Bearcat from External Tools"
description: "Create releases, control uploads and record forum posts through the REST API."
---

The [REST API](/Bearcat/rest-api/) has a few command endpoints. All parameters and response fields are in the [API reference](/Bearcat/api/).

## Commands

| Endpoint | Purpose |
| --- | --- |
| `POST /api/v1/releases` | Create a release from a folder and a release template. |
| `POST /api/v1/uploads/{uploadId}/reupload` | Create a new upload for an offline, partially online, canceled or failed upload. |
| `POST /api/v1/uploads/{uploadId}/cancel` | Cancel a pending or running upload. |
| `POST /api/v1/uploads/{uploadId}/resume` | Queue a canceled upload again. |
| `POST /api/v1/uploads/{uploadId}/check-state` | Check the online state on the hoster now. |
| `POST /api/v1/releases/{releaseId}/posted-locations` | Save the URL where the release was posted. |
| `POST /api/v1/releases/{releaseId}/mark-posted` | Remove the release from the [post queue](/Bearcat/post-queue/). |

Configuration (hosters, templates, rules, credentials) and deleting data are not available through the API.

## API key

`GET` endpoints need no authentication. `POST` endpoints need the header `X-Api-Key` with the value of the setting `Bearcat:ApiKey`. Without a configured key, all commands return `403`.

Generate a key:

```bash
openssl rand -hex 32
```

Restart Bearcat after setting it.

### Docker

Set `BEARCAT_API_KEY` in `.env`:

```text
BEARCAT_API_KEY=your-key
```

Then run `docker compose up -d`.

### Windows service

Add a `Bearcat` section to `%ProgramData%\Bearcat\config.json`:

```json
{
  "Database": { "ConnectionString": "..." },
  "Bearcat": {
    "ApiKey": "your-key"
  }
}
```

Restart the service with `sc stop Bearcat` and `sc start Bearcat`.

`Bearcat.Cli.exe setup` and `Bearcat.Cli.exe set-db-password` keep the section. See [Run Bearcat as a Windows Service](/Bearcat/use-the-windows-service/#where-the-configuration-is-stored).

### Desktop app

Enter the key in the **API key** field of the [Desktop app](/Bearcat/use-the-desktop-launcher/) and click **Save**. If Bearcat is running, click **Stop** and **Start Bearcat**.

### Bearcat.Host started directly

Set the environment variable `Bearcat__ApiKey`, or create `appsettings.user.json` in the working directory:

```json
{
  "Bearcat": {
    "ApiKey": "your-key"
  }
}
```

### Network

The key only protects commands. Anyone who can reach Bearcat can read releases, uploads and download links. Run Bearcat in a trusted network.

The Desktop app and the Windows service listen on `127.0.0.1:17208` only, so the calling tool has to run on the same machine. Docker publishes port `8080`.

## Example workflow

```bash
BEARCAT=http://localhost:8080
API_KEY=your-key
```

### 1. Create the release

```bash
curl -X POST "$BEARCAT/api/v1/releases" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"folderPath": "/mnt/data/releases/Some.Release.2026.1080p", "releaseTemplateId": 3}'
```

- `folderPath`: absolute path as Bearcat sees it. In Docker, this is the container path (`RELEASES_DIR` is mounted at `/mnt/data/releases`).
- `releaseTemplateId`: the number at the end of the template's URL under **Release templates**.
- `name`: optional, defaults to the folder name.
- `primaryLanguageCode`: optional, two letters, for example `de`.

Bearcat does not wait for the folder to become stable. Call this only when the folder is complete. Release info is resolved during the call, which can take a few seconds.

Response: `201 Created` with the release.

### 2. Watch the uploads

Bearcat creates archives and uploads in the background after the [initial upload cooldown](/Bearcat/advanced-configuration/#initial-upload-cooldown).

Uploads of one release:

```bash
curl "$BEARCAT/api/v1/uploads?releaseId=42"
```

`uploadState` is one of `WaitingForArchive`, `Pending`, `Uploading`, `Completed`, `Failed`, `CancellationRequested`, `Canceled`.

Uploads of all releases, finished after a given time (oldest first):

```bash
curl -G "$BEARCAT/api/v1/uploads" \
  --data-urlencode "uploadedAfter=$LAST_UPLOADED_AT" \
  --data-urlencode "pageSize=100"
```

Pass the `uploadedAt` of the last item as `uploadedAfter` in the next call. Use `--data-urlencode`, the timestamp can contain a `+`.

### 3. Find releases to post

```bash
curl "$BEARCAT/api/v1/releases?inPostQueue=true&pageSize=100"
```

Returns the same releases as the [post queue](/Bearcat/post-queue/).

### 4. Get the links

```bash
curl "$BEARCAT/api/v1/releases/42/uploads"
curl "$BEARCAT/api/v1/releases/42/uploads/1234/links?pageSize=100"
curl "$BEARCAT/api/v1/releases/42/uploads/1234/linkcrypter-links"
```

### 5. Record the post

```bash
curl -X POST "$BEARCAT/api/v1/releases/42/posted-locations" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"url": "https://forum.example.com/threads/12345"}'
```

`distributionSiteRegistrationId` and `forumPostTemplateId` are optional.

Response: `201 Created`, or `200 OK` with the existing entry if the release already has this URL.

```bash
curl -X POST "$BEARCAT/api/v1/releases/42/mark-posted" -H "X-Api-Key: $API_KEY"
```

Response: `204 No Content`. The release returns to the post queue when a newer upload completes.

## Status codes

| Code | Meaning |
| --- | --- |
| `400` | Invalid request, for example a relative `folderPath`, a missing folder or an unknown template. `errors` lists the fields. |
| `401` | `X-Api-Key` is missing or wrong. |
| `403` | No API key configured. |
| `404` | Release or upload not found. |
| `409` | The current state does not allow the command, for example the folder is already used or the upload is not canceled. |

Errors are returned as problem details JSON with a `detail` message.
