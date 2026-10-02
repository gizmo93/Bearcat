# Development Notes

This page collects project notes that are useful while working on Bearcat locally.

## Local Configuration

Outside a container, `Bearcat.Host` loads an optional `src/Bearcat.Host/appsettings.user.json` file. Use this file for local paths and credentials that should not be committed, such as:

- `WorkingDirectories`
- `Database:Provider`, `Database:SqliteFilePath`, `Database:ConnectionString`
- `Bearcat:DataDirectory` or `Security:MasterKeyPath` if you want the local encryption key somewhere specific

| Setting | Value |
| --- | --- |
| `Database:Provider` | `Sqlite` or `Postgres`, case-insensitive. Not set: `Postgres`. |
| `Database:SqliteFilePath` | SQLite only. Not set: `bearcat.db` in the data directory. |
| `Database:ConnectionString` | PostgreSQL only. |
| `Database:MigrateOnStartup` | `true` or `false`. Not set: `true` in Production and desktop mode, `false` otherwise. |

Example with SQLite:

```json
{
  "WorkingDirectories": ["/path/to/releases"],
  "Bearcat": {
    "DataDirectory": "/path/to/bearcat-app-data"
  },
  "Database": {
    "Provider": "Sqlite"
  }
}
```

Example with PostgreSQL:

```json
{
  "WorkingDirectories": ["/path/to/releases"],
  "Database": {
    "Provider": "Postgres",
    "ConnectionString": "Host=localhost;Database=<local-database-name>;Username=<db-username>;Password=<db-password>"
  }
}
```

`appsettings.user.json` overrides all other configuration sources, including environment variables and launch profile settings. To run the host on SQLite for one session, use the `http (SQLite)` launch profile. It sets `Database__Provider=Sqlite` and `Database__MigrateOnStartup=true`, so a fresh database is created on the first start. This only takes effect if `appsettings.user.json` does not set these keys.

If no data directory is configured, Bearcat stores `bearcat.key` and the default `bearcat.db` in the operating system's application data folder (`Bearcat` subfolder).

## Formatting

Code is formatted with [CSharpier](https://csharpier.com). The build does not format anything. Instead a pre-commit hook formats the staged files and the pull request workflow checks the formatting. Enable the hook once per clone:

```bash
git config core.hooksPath .githooks
```

The hook only formats staged content, so partially staged files keep their unstaged changes. To format the whole repository manually, run:

```bash
dotnet tool restore
dotnet csharpier format .
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

### Database integration tests

| Project | Covers |
| --- | --- |
| `test/Bearcat.Domain.IntegrationTest` | Domain services against a real database |
| `test/Bearcat.Infrastructure.IntegrationTest` | Repositories and database infrastructure |

- Every database test fixture runs once per provider: `XTest(Postgres)` and `XTest(Sqlite)`.
- Tests run in parallel. Each test gets its own database.
- The PostgreSQL part uses Testcontainers and needs Docker.

To run one provider only, build the project and call the test executable with a filter. Escape the parentheses:

```bash
dotnet build test/Bearcat.Infrastructure.IntegrationTest
test/Bearcat.Infrastructure.IntegrationTest/bin/Debug/net10.0/Bearcat.Infrastructure.IntegrationTest --filter 'FullyQualifiedName~\(Sqlite\)'
```

## OpenAPI Spec

The docs website in `website/` contains an API reference page that renders `website/public/openapi/v1.json`. That file is generated and gitignored, so it is missing after a fresh checkout.

Generate it once before starting the docs dev server:

```bash
bash scripts/generate-openapi-spec.sh
cd website
npm run dev
```

The API reference is then available at `http://localhost:4321/Bearcat/api/`.

The script starts `Bearcat.Host` in OpenAPI spec only mode (`Bearcat:OpenApiSpecOnly`), which maps the API endpoints without touching the database or starting background tasks, downloads the spec and stops the host again. It therefore needs no local database. The docs workflow generates the file the same way before building the website, so the spec is never committed.

## Database Migrations

| Provider | Migrations project |
| --- | --- |
| PostgreSQL | `src/Bearcat.Infrastructure.Migrations.Postgres` |
| SQLite | `src/Bearcat.Infrastructure.Migrations.Sqlite` |

Always create migrations with the script. It adds the migration to both projects and formats them:

```bash
dotnet tool restore
scripts/add-migration.sh <MigrationName>
```

`dotnet tool restore` installs `dotnet-ef` from `.config/dotnet-tools.json`.

Raw SQL in a migration must be written for each provider separately.

Bearcat applies pending migrations on startup in Production and desktop mode, or when `Database:MigrateOnStartup` is `true`. In Development, set `Database__MigrateOnStartup=true` (the `http (SQLite)` profile does this). The pull request workflow fails if the model has changes without a migration in either project. Check locally with:

```bash
dotnet ef migrations has-pending-model-changes --project src/Bearcat.Infrastructure.Migrations.Postgres --startup-project src/Bearcat.Infrastructure.Migrations.Postgres
dotnet ef migrations has-pending-model-changes --project src/Bearcat.Infrastructure.Migrations.Sqlite --startup-project src/Bearcat.Infrastructure.Migrations.Sqlite
```

## Docker

`docker-compose.yml` runs Bearcat with SQLite (`/data/bearcat.db` in the `BEARCAT_DATA_DIR` volume). `docker-compose.postgres.yml` adds a PostgreSQL container and switches Bearcat to it. `docker-compose.build.yml` builds the image locally.

Copy `.env.example` to `.env`, set `RELEASES_DIR`, then run:

| Database | Command |
| --- | --- |
| SQLite | `docker compose up -d` |
| PostgreSQL | `docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d` |

Instead of `-f`, `COMPOSE_FILE=docker-compose.yml:docker-compose.postgres.yml` in `.env` works too (separator `;` on Windows). To build the image from source, add `-f docker-compose.build.yml` and `--build`.

The Docker image sets `ASPNETCORE_ENVIRONMENT=Production` so startup database migrations are enabled when the container runs.

The image is intentionally built and run as `linux/amd64`, even on Macs with Apple Silicon. Bearcat uses the official RAR command line tools, and RAR does not provide an ARM64 Linux build. Since Docker containers on macOS run inside a Linux virtual machine, the container uses the Linux x64 RAR binary.

## Desktop Release Artifacts

The `Release Desktop Artifacts` GitHub workflow builds local desktop packages for:

- macOS Apple Silicon (`osx-arm64`)
- Windows Intel/AMD 64-bit (`win-x64`)
- Windows on Arm (`win-arm64`)

Each package contains the Avalonia launcher and a published `Bearcat.Host` next to it. The host is published with workstation garbage collection for desktop use, while the Docker image keeps the default server GC behavior.

On published GitHub releases, the workflow uploads the ZIP files as release assets. On manual workflow runs, the ZIP files are available as workflow artifacts.

Local desktop artifacts can be generated with:

```bash
scripts/publish-desktop.sh
scripts/publish-desktop.sh win-arm64
```

Without arguments, the script publishes all desktop runtimes: `osx-arm64`, `win-x64`, and `win-arm64`. Pass one or more runtime identifiers to publish only those targets.

The script deletes the target artifact folder, restores runtime-specific assets, publishes `Bearcat.Desktop` and `Bearcat.Host` into separate staging folders, then copies both into `artifacts/desktop/<runtime>`.
For `osx-arm64`, it also marks the native executables as executable, creates `artifacts/desktop/osx-arm64/Bearcat Desktop.app`, and ad-hoc signs the app bundle.

Do not use the timestamp of `Bearcat.Desktop.exe` as the freshness check. For non-single-file .NET publishes, the `.exe` is the native app host stub; the application code is in `Bearcat.Desktop.dll`. MSBuild may preserve timestamps when copying files into the publish folder, so the local script refreshes final artifact timestamps after copying.
