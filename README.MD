<div align="center">
  <img src="docs/images/bearcat.png" alt="bearcat logo" width="400"/>
  <h1 style="margin-top: 0.25rem;">Bearcat</h1>
</div>

Bearcat is a tool for managing One Click Hoster uploads. It tracks release folders and can upload them automatically to configured hosters. Bearcat can also create archives itself in 7Zip and RAR formats.

Generated links can be submitted to one or more link crypters. Bearcat checks link availability automatically, schedules repackaging and reuploading when links go offline, and handles the case where a release that was down must be uploaded again with a `__nonce__` file so the generated archive hashes change.

Bearcat is a .NET 10 application built on ASP.NET Core. The frontend uses Blazor, and PostgreSQL is used as the database. The application can run locally or in Docker.

![gui.png](docs/gui.png)


## Documentation

Additional documentation is kept in [`docs/`](docs/). 

- [User documentation](docs/user-documentation/user-documentation.md)
- [Development notes](docs/development.md)

## Running Bearcat With Docker

The repository contains a `docker-compose.yml` file and `.env` files for local Docker configuration. By default, Compose pulls the public image from GitHub Container Registry instead of building it locally:

```text
ghcr.io/gizmo93/bearcat:latest
```

1. Copy the example environment file:

   ```bash
   cp .env.example .env
   ```

2. Edit `.env` and set at least `RELEASES_DIR` to the local directory that contains the release folders Bearcat should manage. Adjust the PostgreSQL credentials, data directory, exposed port, CPU limits, or memory limits if needed.

3. Start the application and database:

   ```bash
   docker compose up -d
   ```

4. Open Bearcat at:

   ```text
   http://localhost:8080
   ```

To update to the newest published Bearcat image later, run:

```bash
docker compose pull bearcat
docker compose up -d
```

The Compose setup starts Bearcat from the published image, starts PostgreSQL, mounts the configured release directory, and passes the database connection string to the application. Database migrations run when the ASP.NET Core environment is `Production`; the Docker image sets `ASPNETCORE_ENVIRONMENT=Production` by default, and `docker-compose.yml` also sets it explicitly.

The Bearcat image is intentionally built and run as `linux/amd64`, including on Macs with Apple Silicon. Bearcat depends on the official RAR command line tools, and RAR does not provide an ARM64 Linux build because it is closed source. Docker containers run inside a Linux virtual machine on macOS, so the container architecture must match the available Linux RAR binary.

The Compose file hardcodes `platform: linux/amd64` for Bearcat, so `docker compose up -d` and `docker compose pull bearcat` work on Apple Silicon through Docker's amd64 emulation. A direct manual pull needs the platform flag:

```bash
docker pull --platform linux/amd64 ghcr.io/gizmo93/bearcat:latest
```

If you want to build the image locally from the repository instead, use the build override:

```bash
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build
```

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
