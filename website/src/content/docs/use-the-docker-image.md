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

The Docker image includes RAR and 7-Zip and uses **SQLite**, so you only need one container.
You need Docker with Docker Compose. The image supports `linux/amd64` only. Raspberry Pi is not supported.

For Windows or an Apple Silicon Mac, you can use the
[native Desktop app](/Bearcat/use-the-desktop-launcher/).

## 0. Install Docker

### Synology NAS
Install **Container Manager** from the Synology Package Manager.

### Windows
Get [Docker Desktop](https://www.docker.com/products/docker-desktop/) or [Rancher Desktop](https://rancherdesktop.io).

### Linux
On Linux, I recommend [Docker Engine](https://docs.docker.com/engine/install/), including on desktop systems.
It runs directly on the host, avoiding the VM file sharing used by Docker Desktop.

### macOS
You can use Docker Desktop or Rancher Desktop. On macOS, I recommend [OrbStack](https://orbstack.dev),
especially for its fast file access through bind mounts.

## 1. Get the Compose file

Create a folder and download these files into it using **Download raw file** on GitHub:

- [docker-compose.yml](https://github.com/gizmo93/Bearcat/blob/main/docker-compose.yml)
- [.env.example](https://github.com/gizmo93/Bearcat/blob/main/.env.example), renamed to `.env`

## 2. Set your release folder

Open `.env` in a text editor and set `RELEASES_DIR` to an existing folder containing your release
files. Bearcat needs read and write access to it. For example:

```text
RELEASES_DIR=/srv/releases
BEARCAT_DATA_DIR=./bearcat-data
```

Replace `/srv/releases` with your own path. You can leave the other settings unchanged.
Docker does not create a missing release folder. If the folder does not exist, the container does not start.

`BEARCAT_DATA_DIR` stores the database and encryption key in `bearcat-data` next to your Compose
file by default. Keep this data folder on local storage. Release files can be on an NFS or SMB share.

The following settings apply only to Docker Engine running directly on Linux. With Docker Desktop
on Windows, macOS, or Linux, keep the normal bind mount from the Compose file.

With Docker Engine, `rslave` can pass mount changes within `RELEASES_DIR` from the host to the container.
The host mount must be `shared`. If `RELEASES_DIR` itself is remounted, you may need to recreate
the container. See
[Docker's bind propagation documentation](https://docs.docker.com/engine/storage/bind-mounts/#configure-bind-propagation).

In `docker-compose.yml`, under `services.bearcat.volumes`, add `propagation: rslave` to the `bind` section
of the `${RELEASES_DIR}` entry. Keep the data folder entry unchanged.

```yaml
- type: bind
  source: ${RELEASES_DIR:?Set RELEASES_DIR in .env to your release folder}
  target: /mnt/data/releases
  bind:
    create_host_path: false
    propagation: rslave
```

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

Keep SQLite, or follow the steps below to use PostgreSQL.

### Use PostgreSQL instead

1. Save [docker-compose.postgres.yml](https://github.com/gizmo93/Bearcat/blob/main/docker-compose.postgres.yml) next to your Compose file.
2. In `.env`, set `POSTGRES_PASSWORD` to your own password.
3. Add this line to `.env` so all Compose commands use PostgreSQL:

```text
COMPOSE_FILE=docker-compose.yml:docker-compose.postgres.yml
```

On Windows, use `;` between the filenames instead of `:`.
Run `docker compose up -d` to start PostgreSQL in a second container and connect Bearcat to it.

PostgreSQL stores its data in `POSTGRES_DATA_DIR`, which defaults to `./postgres-data`.
Bearcat still needs `BEARCAT_DATA_DIR` for its encryption key.
Switching databases does not transfer data; the new database starts empty.

## Updating Bearcat

With the default `latest` image tag, run these commands from your Compose folder to update Bearcat:

```bash
docker compose pull
docker compose up -d
```

Docker recreates the container if the image has changed. Keep the data folders on your host;
they contain the database and encryption key.

### Upgrading existing Docker installs

Keep your existing Compose files to continue using PostgreSQL. If you replace them with the current
files, which default to SQLite, add this line to `.env` (Windows: `;` instead of `:`):

```text
COMPOSE_FILE=docker-compose.yml:docker-compose.postgres.yml
```

Or start with both files:

```bash
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d
```

Without the override, Bearcat starts with an empty SQLite database. Your PostgreSQL data is still
there. Add the override and run `docker compose up -d` again to switch back.

## Backups

For SQLite, stop Bearcat before copying the database:

```bash
docker compose stop
```

Copy `BEARCAT_DATA_DIR` (normally `bearcat-data`), including `bearcat.db` and `bearcat.key`, then restart:

```bash
docker compose start
```

For a backup while Bearcat runs, use `sqlite3 bearcat-data/bearcat.db ".backup bearcat-backup.db"`
on the host. Copying the database file while it is in use gives an inconsistent backup.

For PostgreSQL, stop both containers, copy `BEARCAT_DATA_DIR` and `POSTGRES_DATA_DIR`, then restart.
Back up release files separately.

Keep `bearcat.key` with your database backup. Without it, Bearcat cannot read your saved account credentials.

## Docker settings

| Variable | Purpose |
| --- | --- |
| `RELEASES_DIR` | Host directory containing your release files. Required. |
| `BEARCAT_DATA_DIR` | Database (`bearcat.db` with SQLite) and encryption key (`bearcat.key`). |
| `BEARCAT_PORT` | Web interface port, normally `8080`. |
| `BEARCAT_API_KEY` | Key for the REST API command endpoints. Empty disables them. See [Control Bearcat from External Tools](/Bearcat/external-orchestration/#api-key). |
| `BEARCAT_TIMEZONE` | Time zone for displayed times, for example `Europe/Berlin`. Empty uses UTC. See [Time zone](/Bearcat/advanced-configuration/#time-zone). |
| `BEARCAT_IMAGE` | Image to run, normally `ghcr.io/gizmo93/bearcat:latest`. Set a version tag or your own image here. |
| `BEARCAT_CPU_LIMIT` | CPU limit in cores, for example `0.5` for half a core. |
| `BEARCAT_MEMORY_LIMIT` | Memory limit, for example `512m` or `1g`. |

### PostgreSQL settings

These settings apply only with `docker-compose.postgres.yml`.

| Variable | Purpose |
| --- | --- |
| `POSTGRES_DATA_DIR` | Host directory for persistent PostgreSQL data. |
| `POSTGRES_USER` | PostgreSQL username. |
| `POSTGRES_PASSWORD` | PostgreSQL password. Choose your own. |
| `POSTGRES_DB` | Database name. |
| `POSTGRES_PORT` | Host port for database connections, normally `5432`. Change it if that port is already in use. |
| `POSTGRES_CPU_LIMIT` | CPU limit in cores, for example `0.5` for half a core. |
| `POSTGRES_MEMORY_LIMIT` | Memory limit, for example `512m` or `1g`. |
