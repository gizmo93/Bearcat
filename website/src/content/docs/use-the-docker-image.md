---
title: "Run Bearcat in Docker"
description: "Host Bearcat with Docker on Linux, NAS devices, servers, Windows, or macOS."
---

Docker is the recommended setup for Linux, NAS, and server deployments.

On Windows, prefer the [Windows service](/Bearcat/use-the-windows-service/) for always-on use or the [Desktop app](/Bearcat/use-the-desktop-launcher/) for on-demand use. Docker also works, but reading release files through its Linux VM is slower than with the native options.

On Apple Silicon Macs, prefer the native [Desktop app](/Bearcat/use-the-desktop-launcher/). The Docker image uses `linux/amd64` because its bundled RAR tools require Linux x64.

## Initial setup

### Windows
Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) or [Rancher Desktop](https://rancherdesktop.io).

### macOS
Install [Docker Desktop](https://www.docker.com/products/docker-desktop/), [Rancher Desktop](https://rancherdesktop.io), or [OrbStack](https://orbstack.dev). I use OrbStack to run containers while developing Bearcat on macOS.

![Bearcat running in OrbStack on macOS](images/bearcat-orbstack.png)

### Linux
Docker containers run directly on Linux. Docker Desktop and Rancher Desktop are also available; follow their documentation for installation.

### Synology NAS
On a supported x86_64 Synology NAS, install **Container Manager** from Package Manager.


## Running Bearcat

Use the repository's `docker-compose.yml` and copy `.env.example` to `.env`.
Before starting the containers:

1. Set `RELEASES_DIR` to your release folder. It must exist and allow Bearcat to read and write files. Mounted network folders work too.
2. Choose a `POSTGRES_PASSWORD`.
3. Check `POSTGRES_DATA_DIR` and `BEARCAT_DATA_DIR`. These folders keep your database and application data between container updates.

The remaining settings control ports, database names, resource limits, and the image version.

### Database related

| Variable | Purpose |
| --- | --- |
| `POSTGRES_DATA_DIR` | Host directory for persistent PostgreSQL data. |
| `POSTGRES_USER` | PostgreSQL username. |
| `POSTGRES_PASSWORD` | PostgreSQL password. |
| `POSTGRES_DB` | Database name. |
| `POSTGRES_PORT` | Host port for database connections, normally `5432`. Change it if that port is already in use. |
| `POSTGRES_CPU_LIMIT` | CPU limit in cores, for example `0.5` for half a core. |
| `POSTGRES_MEMORY_LIMIT` | Memory limit, for example `512m` or `1g`. |

### Bearcat related

| Variable | Purpose |
| --- | --- |
| `RELEASES_DIR` | Host directory containing your release files. Required. |
| `BEARCAT_DATA_DIR` | Persistent application data, including the `bearcat.key` encryption key created on first start. |
| `BEARCAT_PORT` | Web interface port, normally `8080`. |
| `BEARCAT_IMAGE` | Image to run. Defaults to `ghcr.io/gizmo93/bearcat:latest`; change it to select a version or your own image. |
| `BEARCAT_CPU_LIMIT` | CPU limit in cores, for example `0.5` for half a core. |
| `BEARCAT_MEMORY_LIMIT` | Memory limit, for example `512m` or `1g`. |

From the directory containing `docker-compose.yml`, run:

```bash
docker compose up -d
```

This starts Bearcat and PostgreSQL using your `.env` settings. The first start may take a while.


## Updating Bearcat

The example `.env` uses the `latest` image tag. To download and start a newer image, run these commands from the directory containing `docker-compose.yml`:

```bash
docker compose pull
docker compose up -d
```

This will only recreate the container if the newer image is different from the one you already have.
As Bearcat's database and application data directory are both mounted to persistent locations on your host machine, you won't lose any data by updating the container image.

## Account data encryption key

Bearcat stores hoster, link crypter, and NFO database account configurations encrypted in the database. The encryption key is not part of the Docker image. On first start, Bearcat creates a random key file here:

```text
${BEARCAT_DATA_DIR:-./bearcat-data}/bearcat.key
```

Keep this file. Without `bearcat.key`, Bearcat cannot decrypt the stored account configurations anymore.

For backups and server moves, copy both:

- the PostgreSQL data directory, usually `${POSTGRES_DATA_DIR:-./postgres-data}`
- the Bearcat application data directory, usually `${BEARCAT_DATA_DIR:-./bearcat-data}`

On the first start of a version with encrypted account data, Bearcat automatically migrates existing plaintext registration configs to encrypted values.
