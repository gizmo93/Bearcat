---
title: "PostgreSQL Setup"
description: "Install and run PostgreSQL for Bearcat, for the Desktop app or the Windows service."
---

Bearcat needs a PostgreSQL database to store its data.

It does not ship with PostgreSQL, so you need to provide it yourself. Both the Desktop app and the [Windows service](/Bearcat/use-the-windows-service/) connect to your own PostgreSQL server.

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

## Windows

On Windows you have two ways to run PostgreSQL: in Docker, or with the native installer.

:::tip[Recommended on Windows]
Run PostgreSQL in Docker (see [PostgreSQL In Docker](#postgresql-in-docker) below). It keeps the database self-contained, easy to back up, and easy to remove again. The native installer registers an always-running Windows service and a system-wide install that is more tedious to get rid of later.
:::

If you prefer a native install, follow the steps below.

### Native installer

Download the PostgreSQL 18 Windows installer from [postgresql.org/download/windows](https://www.postgresql.org/download/windows/).

During installation:

- Choose PostgreSQL 18.
- Keep the default port `5432` unless it is already used.
- The installer creates a superuser named `postgres` and asks you to set its password. You choose only the password here, not the user name.
- Stack Builder is optional and not required by Bearcat.

After installation you need a user and a database for Bearcat. You have two options:

**Use the `postgres` superuser directly (simplest).** In your Bearcat configuration, enter `postgres` as the username and the password you set during installation, and `bearcat` as the database name. Bearcat creates the database and applies migrations on first start.

**Create a dedicated `bearcat` user.** Open **pgAdmin 4** (installed alongside PostgreSQL), connect as `postgres`, and create a new login role named `bearcat`: set a password and enable **Can create databases?** in its privileges. Then use that account in your Bearcat configuration. Bearcat creates the `bearcat` database on first start.

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

## PostgreSQL In Docker

If you prefer not to install PostgreSQL directly on your operating system, you can run only PostgreSQL in Docker and still run Bearcat natively with the Desktop app or the Windows service. On Windows this is the recommended way to run PostgreSQL.

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
  -v ~/Bearcat/postgres-data:/var/lib/postgresql/data \
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
  -v "$env:USERPROFILE\Bearcat\postgres-data:/var/lib/postgresql/data" `
  postgres:18
```

Use these settings in your Bearcat configuration:

```text
Host: localhost
Port: 5432
Database: bearcat
Username: bearcat
Password: choose-a-password
```

To stop the database:

```bash
docker stop bearcat-postgres
```

To start it again later:

```bash
docker start bearcat-postgres
```

Do not delete the data directory unless you intentionally want to delete the Bearcat database. For backups, stop the container first so PostgreSQL has flushed all files cleanly.

## Troubleshooting

If Bearcat cannot connect:

- Check that PostgreSQL is running.
- Check that the port is `5432`, or update your Bearcat configuration to use the port you chose.
- Check that the username and password match.
- If the database does not exist, either grant the user `CREATEDB` permission or create the database manually.
