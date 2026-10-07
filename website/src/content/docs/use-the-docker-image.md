---
title: "Run Bearcat in Docker"
description: "Start Bearcat with Docker Compose and SQLite on a Linux computer, NAS, or server."
prev:
  label: "Choose an installation"
  link: "/Bearcat/#get-started"
next:
  label: "Your first upload"
  link: "/Bearcat/post-installation/"
---

The Docker image includes the archive tools. The default setup uses **SQLite**, so it runs as
one container and needs no database server.

You need Docker with Docker Compose.
The Bearcat Docker image is only available for the architecture `linux/amd64`. For Windows or an Apple Silicon Mac, you can use the
[native Desktop app](/Bearcat/use-the-desktop-launcher/).

## 0. Install Docker (skip if it's already installed)

### Synology NAS
Install **Container Manager** from the Synology Package Manager.

### Windows, Linux Desktop
Get [Docker Desktop](https://www.docker.com/products/docker-desktop/) or [Rancher Desktop](https://rancherdesktop.io).

### Linux (command line)
If you are using a Linux server with only the command line, [you can just install the Docker Engine](https://docs.docker.com/engine/install/).

### macOS
Docker Desktop and Rancher Desktop can be used (links above in the "Windows, Linux Desktop" section).
Personally I recommend [OrbStack](https://orbstack.dev) on macOS as it has the best performance overall and near native IO performance for bind mounts.

## 1. Get the Compose file

Create a folder for the setup. Save these two files from the Bearcat repository into it:

- [docker-compose.yml](https://github.com/gizmo93/Bearcat/blob/main/docker-compose.yml)
- [.env.example](https://github.com/gizmo93/Bearcat/blob/main/.env.example), renamed to `.env`

Use GitHub's **Download raw file** button to save each file's contents.

## 2. Set your release folder

Open `.env` in a text editor and set `RELEASES_DIR` to an existing folder containing your release
files. Bearcat needs read and write access to it. For example:

```text
RELEASES_DIR=/srv/releases
BEARCAT_DATA_DIR=./bearcat-data
```

Replace `/srv/releases` with your own path. You can leave the other settings unchanged.

`BEARCAT_DATA_DIR` keeps the database and encryption key. The default creates `bearcat-data` next
to your Compose file. Keep this folder on local storage, not an NFS or SMB share.
Your release files can be on a mounted network share.

## 3. Start Bearcat

Open a terminal in the folder containing `docker-compose.yml` and run:

```bash
docker compose up -d
```

Open [http://localhost:8080](http://localhost:8080). If Docker runs on another computer or a NAS,
open `http://<server-address>:8080` instead. The first start may take a moment.

**Next: [Add your hoster account and upload your first release](/Bearcat/post-installation/).**

In Bearcat's folder picker, your release folder appears as `/mnt/data/releases`.

## Database

SQLite is already configured. Use the following option only if you want PostgreSQL.

### Use PostgreSQL instead

1. Save [docker-compose.postgres.yml](https://github.com/gizmo93/Bearcat/blob/main/docker-compose.postgres.yml) next to your Compose file.
2. In `.env`, set `POSTGRES_PASSWORD` to your own password.
3. Add this line to `.env` so all Compose commands use PostgreSQL:

```text
COMPOSE_FILE=docker-compose.yml:docker-compose.postgres.yml
```

On Windows, use `;` between the filenames instead of `:`.
Then run `docker compose up -d`. This starts a PostgreSQL server in a second container and connects Bearcat to it.

PostgreSQL stores its data in `POSTGRES_DATA_DIR`, which defaults to `./postgres-data`.
Bearcat still needs `BEARCAT_DATA_DIR` for its encryption key.
Changing the database type does not transfer existing data. A new database starts empty.

## Updating Bearcat

The example `.env` uses the `latest` image tag. To download and start a newer image, run these commands from the directory containing `docker-compose.yml`:

```bash
docker compose pull
docker compose up -d
```

This will only recreate the container if the newer image is different from the one you already have.
The database and encryption key stay in the data folders on your host. Keep these folders when updating.

### Upgrading existing Docker installs

Older versions of `docker-compose.yml` started PostgreSQL. The current `docker-compose.yml` runs Bearcat with SQLite. If you keep your existing compose files, nothing changes.

If your installation uses PostgreSQL and you replace your compose files with the current ones, add this line to `.env` (Windows: `;` instead of `:`):

```text
COMPOSE_FILE=docker-compose.yml:docker-compose.postgres.yml
```

Or start with both files:

```bash
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d
```

Without the PostgreSQL override, Bearcat starts with an empty SQLite database. Your data is still in PostgreSQL. Add the override and run `docker compose up -d` again to switch back.

## Backups

With the default SQLite setup, run:

```bash
docker compose stop
```

Copy the entire `BEARCAT_DATA_DIR` folder, normally `bearcat-data`, to your backup location.
It contains both `bearcat.db` and `bearcat.key`. Then start Bearcat again:

```bash
docker compose start
```

To back up the database while Bearcat runs, use `sqlite3 bearcat-data/bearcat.db ".backup bearcat-backup.db"` on the host instead. Copying `bearcat.db` while Bearcat runs does not give a consistent backup.

With PostgreSQL, stop both containers before copying `BEARCAT_DATA_DIR` and `POSTGRES_DATA_DIR`,
then start them again. These backups contain Bearcat's settings and records. Back up your release
files separately if you need them too.

Keep `bearcat.key` with your database backup. Without it, Bearcat cannot read your saved account credentials.

## Docker settings

| Variable | Purpose |
| --- | --- |
| `RELEASES_DIR` | Host directory containing your release files. Required. |
| `BEARCAT_DATA_DIR` | Persistent application data: the `bearcat.key` encryption key created on first start, and `bearcat.db` with SQLite. |
| `BEARCAT_PORT` | Web interface port, normally `8080`. |
| `BEARCAT_API_KEY` | Key for the REST API command endpoints. Empty disables them. See [Orchestrate Bearcat from External Tools](/Bearcat/external-orchestration/#api-key). |
| `BEARCAT_TIMEZONE` | Time zone for displayed times, for example `Europe/Berlin`. Empty uses UTC. See [Time zone](/Bearcat/advanced-configuration/#time-zone). |
| `BEARCAT_IMAGE` | Image to run. Defaults to `ghcr.io/gizmo93/bearcat:latest`; change it to select a version or your own image. |
| `BEARCAT_CPU_LIMIT` | CPU limit in cores, for example `0.5` for half a core. |
| `BEARCAT_MEMORY_LIMIT` | Memory limit, for example `512m` or `1g`. |

### PostgreSQL settings

Only used with `docker-compose.postgres.yml`.

| Variable | Purpose |
| --- | --- |
| `POSTGRES_DATA_DIR` | Host directory for persistent PostgreSQL data. |
| `POSTGRES_USER` | PostgreSQL username. |
| `POSTGRES_PASSWORD` | PostgreSQL password. Choose your own. |
| `POSTGRES_DB` | Database name. |
| `POSTGRES_PORT` | Host port for database connections, normally `5432`. Change it if that port is already in use. |
| `POSTGRES_CPU_LIMIT` | CPU limit in cores, for example `0.5` for half a core. |
| `POSTGRES_MEMORY_LIMIT` | Memory limit, for example `512m` or `1g`. |
