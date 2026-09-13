<div align="center">
  <img src="docs/images/bearcat.png" alt="bearcat logo" width="400"/>
  <h1 style="margin-top: 0.25rem;">Bearcat: Self-hosted upload manager for one-click hosters</h1>
</div>

Bearcat manages uploads to Rapidgator, DDownload, Keep2Share, and other one-click hosters. It watches release folders, creates RAR or 7z archives, uploads them, and checks whether the links are still online. When links go offline, it can repackage and reupload the files automatically.

Run it on your own desktop, NAS, or server and manage it through a web interface.

[Installation](#installation) · [User documentation](https://gizmo93.github.io/Bearcat/) · [Downloads](https://github.com/gizmo93/Bearcat/releases)

![Bearcat web interface for managing releases and uploads](docs/images/gui.png)

## Features

- Create RAR or 7z archives, or upload archives you already have.
- Upload releases to multiple hosters and check link availability automatically.
- Repackage and reupload managed releases when links go offline.
- Send upload links to Filecrypt, Hide.cx, Keeplinks, or tolink.to.
- Fetch release and media metadata, upload cover images, and generate BBCode forum posts.
- Track releases waiting to be posted and prepare forum drafts for you to review and submit.
- Forward upload notifications and errors to Telegram.

See the [supported hosters and link crypters](https://gizmo93.github.io/Bearcat/#which-hosters-and-linkcrypters-are-supported) for the full list.

## Installation

- [Docker](https://gizmo93.github.io/Bearcat/use-the-docker-image/): run Bearcat on Linux, a NAS, or a server.
- [Desktop app](https://gizmo93.github.io/Bearcat/use-the-desktop-launcher/): start and stop Bearcat on Windows or macOS.
- [Windows service](https://gizmo93.github.io/Bearcat/use-the-windows-service/): keep Bearcat running in the background, including when no user is logged in.

After installation, follow the [initial setup guide](https://gizmo93.github.io/Bearcat/post-installation/) to connect hoster accounts and configure your first release.

## Documentation

- [User guide](https://gizmo93.github.io/Bearcat/)
- [Archives, link checks, and automatic reuploads](https://gizmo93.github.io/Bearcat/upload-lifecycle/)
- [Managed releases and existing archives](https://gizmo93.github.io/Bearcat/release-types/)
- [Development notes](docs/development.md)

## Local development

Bearcat uses .NET 10, ASP.NET Core, Blazor, and PostgreSQL.

Install the .NET 10 SDK version declared in `global.json`, then restore and build the solution from the repository root:

```bash
dotnet restore Bearcat.slnx
dotnet build Bearcat.slnx
```

Run the test suite with:

```bash
dotnet test Bearcat.slnx
```

For local development, Bearcat can load `appsettings.user.json` in development mode. Use it for machine-specific settings such as local database credentials and release directories. This file is ignored by Git and should not be committed.

Example for `Bearcat.Host/appsettings.user.json`:

```json
{
  "ReleaseDataDirectory": "/path/to/releases",
  "Database": {
    "ConnectionString": "Host=localhost;Database=<your-local-db-name>;Username=<db-username>;Password=<db-password>>"
  }
}
```

The main host application is `Bearcat.Host`. The Blazor UI project is `Bearcat.Website`.
