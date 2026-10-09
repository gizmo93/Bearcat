---
title: "Set Up PostgreSQL for Bearcat"
description: "Install PostgreSQL for the Desktop app or Windows service."
---

Follow this guide if you choose **PostgreSQL** in the [Desktop app](/Bearcat/use-the-desktop-launcher/)
or [Windows service](/Bearcat/use-the-windows-service/). The default SQLite setup needs no database server.

For Docker, `docker-compose.postgres.yml` starts PostgreSQL in its own container. See [Run Bearcat in Docker](/Bearcat/use-the-docker-image/#database).

Use PostgreSQL 18. Older PostgreSQL versions are currently untested.

## Recommended Settings

Use these connection settings in Bearcat:

```text
Host: localhost
Port: 5432
Database: bearcat
Username: bearcat
Password: choose-a-password
```

Choose your own password. If you change any other values, enter them in Bearcat too.

## PostgreSQL In Docker

On Windows, Docker is the recommended way to run PostgreSQL. Bearcat can still run as the Desktop app or Windows service.

Create a folder for the database:

```bash
mkdir -p ~/Bearcat/postgres-data
```

This is the database folder. Include it in your backups.

Start PostgreSQL 18:

```bash
docker run -d \
  --name bearcat-postgres \
  -e POSTGRES_USER=bearcat \
  -e POSTGRES_PASSWORD=choose-a-password \
  -e POSTGRES_DB=bearcat \
  -p 5432:5432 \
  -v ~/Bearcat/postgres-data:/var/lib/postgresql \
  postgres:18
```

On Windows PowerShell, use a Windows path instead:

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\Bearcat\postgres-data"

docker run -d `
  --name bearcat-postgres `
  -e POSTGRES_USER=bearcat `
  -e POSTGRES_PASSWORD=choose-a-password `
  -e POSTGRES_DB=bearcat `
  -p 5432:5432 `
  -v "$env:USERPROFILE\Bearcat\postgres-data:/var/lib/postgresql" `
  postgres:18
```

Enter the [connection settings above](#recommended-settings) in Bearcat, using the password you chose for the container.

To stop the database:

```bash
docker stop bearcat-postgres
```

To start it again later:

```bash
docker start bearcat-postgres
```

Stop the container before copying the data folder for a backup. Deleting this folder deletes the Bearcat database.

## Windows

As an alternative to [Docker](#postgresql-in-docker), install PostgreSQL as a Windows service:

### Native installer

Download the PostgreSQL 18 Windows installer from [postgresql.org/download/windows](https://www.postgresql.org/download/windows/).

During installation:

- Choose PostgreSQL 18.
- Keep the default port `5432` unless it is already used.
- Set a password for the `postgres` superuser created by the installer.
- Stack Builder is optional and not required by Bearcat.

Choose the account Bearcat should use:

- **Installer account:** Enter `postgres` and the password you chose during installation in Bearcat. This uses the PostgreSQL superuser.
- **Separate account:** Open **pgAdmin 4**, connect as `postgres`, and create a login role named `bearcat`. Set a password and enable **Can create databases?** Then enter this account in Bearcat.

Set the database name to `bearcat`. Bearcat creates it and applies migrations on first start.

## macOS

Download Postgres.app from [postgresapp.com](https://postgresapp.com).

Install and start Postgres.app, then create or start a PostgreSQL 18 server. Keep the default port `5432` unless it is already used.

Use Postgres.app’s `psql` command in a terminal. If your shell cannot find it, follow the
Postgres.app instructions to add its command line tools to `PATH` or use the full path.

Create an account with permission to create the database:

```bash
psql postgres
```

```sql
CREATE USER bearcat WITH PASSWORD 'choose-a-password';
ALTER USER bearcat CREATEDB;
```

To create the database manually instead:

```bash
psql postgres
```

```sql
CREATE USER bearcat WITH PASSWORD 'choose-a-password';
CREATE DATABASE bearcat OWNER bearcat;
```

Then enter the connection settings in Bearcat.

## Troubleshooting

If Bearcat cannot connect:

- Check that PostgreSQL is running.
- Check that the port in Bearcat matches the PostgreSQL port (default: `5432`).
- Check that the username and password match.
- If the database does not exist, either grant the user `CREATEDB` permission or create the database manually.
