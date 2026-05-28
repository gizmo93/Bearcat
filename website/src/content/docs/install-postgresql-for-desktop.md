---
title: "PostgreSQL for Desktop"
description: "Install and configure PostgreSQL for the Bearcat Desktop app."
---

The Bearcat Desktop app needs a PostgreSQL database to store its data.

It does not ship with PostgreSQL, so you need to install it on your own.

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

You can use different values, but enter the same values in the Bearcat Desktop app.

## Windows Native

Download the PostgreSQL 18 Windows installer from:

```text
https://www.postgresql.org/download/windows/
```

During installation:

- Choose PostgreSQL 18.
- Keep the default port `5432` unless it is already used.
- Set a password for the PostgreSQL `postgres` administrator user.
- Stack Builder is optional and not required by Bearcat.

Then enter `bearcat` and the created user / password as the database name in the Desktop app. Bearcat will create the database and apply migrations on startup.

## macOS Native

Download Postgres.app from:

```text
https://postgresapp.com
```

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

Enter the same host, port, database, username, and password in the Bearcat Desktop app.

## PostgreSQL In Docker

If you prefer not to install PostgreSQL directly on your desktop OS, you can run only PostgreSQL in Docker and still use the Bearcat Desktop app.

Create a persistent data directory on your host machine:

```bash
mkdir -p ~/Bearcat/postgres-data
```

Choose a folder that is somewhere, where you can easily backup it, if you want to. This folder contains the PostgreSQL database files.

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

Use these settings in the Bearcat Desktop app:

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

If the Desktop app cannot connect:

- Check that PostgreSQL is running.
- Check that the port is `5432`, or update the Desktop app to use the port you chose.
- Check that the username and password match.
- If the database does not exist, either grant the user `CREATEDB` permission or create the database manually.
