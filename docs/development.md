# Development Notes

This page collects project notes that are useful while working on Bearcat locally.

## Local Configuration

In development mode, `Bearcat.Host` loads an optional `appsettings.user.json` file. Use this file for local paths and credentials that should not be committed, such as:

- `ReleaseDataDirectory`
- `Database:ConnectionString`
- `Bearcat:DataDirectory` or `Security:MasterKeyPath` if you want the local encryption key somewhere specific

Example:

```json
{
  "ReleaseDataDirectory": "/path/to/releases",
  "Bearcat": {
    "DataDirectory": "/path/to/bearcat-app-data"
  },
  "Database": {
    "ConnectionString": "Host=localhost;Database=<local-database-name>;Username=<db-username>;Password=<db-password>"
  }
}
```

If no key path or data directory is configured, Bearcat stores `bearcat.key` in the operating system's application data folder.

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
dotnet ef migrations add <migration-name> --startup-project .
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
