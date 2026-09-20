---
title: "Run Bearcat in Docker"
description: "Host Bearcat with Docker on Linux, NAS devices, servers, Windows, or macOS."
---

Docker is the recommended setup for Linux, NAS, and server deployments.

On Windows, Docker still works, but it is no longer the recommended option: prefer the [Windows service](/Bearcat/use-the-windows-service/) for always-on use or the [Desktop app](/Bearcat/use-the-desktop-launcher/) for on-demand use. On Windows the container reads your release files through a bind mount into a Linux VM, which is noticeably slower than the native Windows options. Use Docker on Windows only if you explicitly want a container-based setup.

For local desktop use on macOS Apple Silicon, prefer the Bearcat Desktop app. The Docker image is published as `linux/amd64` because the official RAR command line tools are only available for Linux x64, while the Desktop app can run natively on Apple Silicon.

## Initial setup

### Windows
To run local Docker containers, I recommend to install Docker Desktop (https://www.docker.com/products/docker-desktop/) or Rancher Desktop (https://rancherdesktop.io).
Docker Desktop is faster and easier to set up than Rancher and it's free for non-commercial use.

## macOS
On macOS, you can also use Docker Desktop (https://www.docker.com/products/docker-desktop/) or Rancher Desktop (https://rancherdesktop.io) or OrbStack (https://orbstack.dev).
All of these options work, however, I personally recommend OrbStack.
It's designed to be macOS-only and thus it has the best performance and lowest memory footprint. The free version should be enough to run Bearcat.
Bearcat is developed on macOS and I personally use OrbStack to run containers locally.

![Bearcat running in OrbStack on macOS](images/bearcat-orbstack.png)

## Linux
Docker Desktop and Rancher Desktop are also available for Linux.
For futher infos on how to set them up, please check their documentation.
As Docker is a Linux native technology, Docker containers basically run "natively" on Linux, while on Windows and macOS a small Linux VM runs these containers.

## Synology NAS
Synology NAS with an x86_64 CPU architecture support Docker ("Container Manager").
You just need to install the respective package from the Synology Package Manager.


## Running Bearcat

To get started with Docker, you can use the provided `docker-compose.yml` file.
Copy the `.env.example` file to `.env` and set the variables in that file according to your needs.

These are the variables you need to set and what they are for:

### Database related

- `POSTGRES_DATA_DIR`: The directory on the host machine where PostgreSQL data will be stored. This allows you to persist your database data even if the container is removed. Also, it allows you to create backups of your database
- `POSTGRES_USER`: The username for the PostgreSQL database
- `POSTGRES_PASSWORD`: The password for the PostgreSQL database
- `POSTGRES_DB`: The name of the PostgreSQL database
- `POSTGRES_CPU_LIMIT`: Optional CPU limit for the PostgreSQL container, measured in CPU cores. For example, `0.5` limits it to half a core and `1` to one core.
- `POSTGRES_MEMORY_LIMIT`: Optional memory limit for the PostgreSQL container, for example `512m` for 512 megabytes or `1g` for 1 gigabyte.
- `POSTGRES_PORT`: The host port for connections to PostgreSQL from outside the Docker network. Defaults to `5432`. Choose another port if it is already in use.

### Bearcat related
- `BEARCAT_PORT`: The port on which Bearcat web frontend will be accessible. If you don't set this, Bearcat will run at port 8080.
- `RELEASES_DIR`: Required path to your release files on the host machine. Mounted network folders work too. The directory must exist, and Bearcat needs read and write access to it.
- `BEARCAT_DATA_DIR`: The directory on the host machine where Bearcat stores application data that must survive container updates. Bearcat creates `bearcat.key` there on first start. This key is required to decrypt stored hoster, link crypter, and NFO database account configurations.
- `BEARCAT_IMAGE`: The Docker image to run. Defaults to `ghcr.io/justanotherx265/bearcat:latest` from GitHub Container Registry. Change it to use a specific version or your own image.
- `BEARCAT_CPU_LIMIT`: Optional CPU limit for the Bearcat container, measured in CPU cores. For example, `0.5` limits it to half a core and `1` to one core.
- `BEARCAT_MEMORY_LIMIT`: Optional memory limit for the Bearcat container, for example `512m` for 512 megabytes or `1g` for 1 gigabyte.


Then run the following command in the terminal from the directory where the `docker-compose.yml` file is located:

```bash
docker compose up -d
```

That one will start the Bearcat and PostgreSQL container with the settings you did in your .env file.
Depending on the performance of your machine it might take a while and the command will in the end tell you, if all containers could be started successfully.


## Updating Bearcat

The default .env file uses the latest image of Bearcat, the moment you run docker compose up.
If there is a new version out and you want to update your instance, execute the following command in the terminal from the directory where the `docker-compose.yml` file is located:

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
