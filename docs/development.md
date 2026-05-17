# Development Notes

This page collects project notes that are useful while working on Bearcat locally.

## Local Configuration

In development mode, `Bearcat.Host` loads an optional `appsettings.user.json` file. Use this file for local paths and credentials that should not be committed, such as:

- `ReleaseDataDirectory`
- `Database:ConnectionString`

Example:

```json
{
  "ReleaseDataDirectory": "/path/to/releases",
  "Database": {
    "ConnectionString": "Host=localhost;Database=<local-database-name>;Username=<db-username>;Password=<db-password>"
  }
}
```

## Tests

Run all tests from the repository root:

```bash
dotnet test Bearcat.slnx
```

Focused test projects can also be run directly, for example:

```bash
dotnet test test/Bearcat.Domain.UnitTest/Bearcat.Domain.UnitTest.csproj
```

## Database Migrations

Entity Framework migrations are created from the infrastructure project:

```bash
cd src/Bearcat.Infrastructure
dotnet ef migrations add <migration-name>
```

Apply migrations to the local database with:

```bash
dotnet ef database update
```

## Docker

Docker Compose starts Bearcat together with PostgreSQL. Copy `.env.example` to `.env`, set `RELEASES_DIR`, then run:

```bash
docker compose up --build
```

The Docker image sets `ASPNETCORE_ENVIRONMENT=Production` so startup database migrations are enabled when the container runs.

The image is intentionally built and run as `linux/amd64`, even on Macs with Apple Silicon. Bearcat uses the official RAR command line tools, and RAR does not provide an ARM64 Linux build. Since Docker containers on macOS run inside a Linux virtual machine, the container uses the Linux x64 RAR binary.
