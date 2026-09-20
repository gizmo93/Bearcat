---
title: "Set Up PostgreSQL for Bearcat"
description: "Install and run PostgreSQL for Bearcat, for the Desktop app or the Windows service."
---

Bearcat needs a PostgreSQL database to store its data.

Both the Desktop app and the [Windows service](/Bearcat/use-the-windows-service/) connect to a PostgreSQL server you install separately.

Use PostgreSQL 18. Older PostgreSQL versions are currently untested.

## Recommended Settings

The examples below use these values:

```text
Host: localhost
Port: 5432
Database: bearcat
Username: bearcat
Password: choose-a-password
```

You can use different values, but enter the same values in your Bearcat configuration.

## PostgreSQL In Docker

I recommend Docker for PostgreSQL on Windows. Bearcat itself can still run natively as the Desktop app or Windows service.

Create a persistent data directory on your host machine:

```bash
mkdir -p ~/Bearcat/postgres-data
```

Choose a folder you can easily back up. This folder contains the PostgreSQL database files.

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

Do not delete the data directory unless you intentionally want to delete the Bearcat database. For backups, stop the container first so PostgreSQL has flushed all files cleanly.

## Windows

Use [PostgreSQL in Docker](#postgresql-in-docker) above, or follow these steps for a native installation. The native installer runs PostgreSQL as a Windows service.

### Native installer

Download the PostgreSQL 18 Windows installer from [postgresql.org/download/windows](https://www.postgresql.org/download/windows/).

During installation:

- Choose PostgreSQL 18.
- Keep the default port `5432` unless it is already used.
- The installer creates a superuser named `postgres` and asks you to set its password. You choose only the password here, not the user name.
- Stack Builder is optional and not required by Bearcat.

Choose the account Bearcat should use:

- **Installer account:** Enter `postgres` and the password you chose during installation in Bearcat. This uses the PostgreSQL superuser.
- **Separate account:** Open **pgAdmin 4**, connect as `postgres`, and create a login role named `bearcat`. Set a password and enable **Can create databases?** Then enter this account in Bearcat.

Set the database name to `bearcat`. Bearcat creates it and applies migrations on first start.

## macOS

Download Postgres.app from [postgresapp.com](https://postgresapp.com).

Install and start Postgres.app, then create or start a PostgreSQL 18 server. Keep the default port `5432` unless it is already used.

Open a terminal and use the `psql` command shipped with Postgres.app. If `psql` is not on your `PATH`, use the full path from the Postgres.app documentation or add the Postgres.app command line tools to your shell profile.

To let Bearcat create the database on first start:

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

Enter the same host, port, database, username, and password in your Bearcat configuration.

## Troubleshooting

If Bearcat cannot connect:

- Check that PostgreSQL is running.
- Check that the port is `5432`, or update your Bearcat configuration to use the port you chose.
- Check that the username and password match.
- If the database does not exist, either grant the user `CREATEDB` permission or create the database manually.
