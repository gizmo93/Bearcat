<div align="center">
  <img src="docs/images/bearcat.png" alt="bearcat logo" width="400"/>
  <h1 style="margin-top: 0.25rem;">Bearcat</h1>
</div>

Bearcat is a tool for managing One Click Hoster uploads. It tracks release folders and can upload them automatically to configured hosters. Bearcat can also create archives itself in 7Zip and RAR formats.

Generated links can be submitted to one or more link crypters. Bearcat checks link availability automatically, schedules repackaging and reuploading when links go offline, and handles the case where a release that was down must be uploaded again with a `__nonce__` file so the generated archive hashes change.

Bearcat is a .NET 10 application built on ASP.NET Core. The frontend uses Blazor, and PostgreSQL is used as the database. 

The application can run in [Docker](https://gizmo93.github.io/Bearcat/use-the-docker-image/), as a [Desktop App on macOS and Windows](https://gizmo93.github.io/Bearcat/use-the-desktop-launcher/) and as a [Windows Service](https://gizmo93.github.io/Bearcat/use-the-windows-service/) for Server usages.

![gui.png](docs/images/gui.png)


## Documentation

- [User documentation](https://gizmo93.github.io/Bearcat)
- [Development notes](docs/development.md)

## Running Bearcat With Docker

Check the [user documentation](https://gizmo93.github.io/Bearcat/use-the-docker-image/) for instructions on how to run Bearcat with Docker.

## Local development

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
