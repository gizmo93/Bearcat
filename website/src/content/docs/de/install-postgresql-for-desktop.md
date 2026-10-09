---
title: "PostgreSQL für Bearcat einrichten"
description: "PostgreSQL für die Desktopanwendung oder den Windows-Dienst installieren."
---

Folge dieser Anleitung, wenn du in der [Desktopanwendung](/Bearcat/de/use-the-desktop-launcher/)
oder im [Windows-Dienst](/Bearcat/de/use-the-windows-service/) **PostgreSQL** wählst.
Die Standardeinrichtung mit SQLite braucht keinen Datenbankserver.

Für Docker startet `docker-compose.postgres.yml` PostgreSQL in einem eigenen Container. Siehe [Bearcat in Docker ausführen](/Bearcat/de/use-the-docker-image/#datenbank).

Verwende PostgreSQL 18. Ältere PostgreSQL-Versionen sind derzeit ungetestet.

## Empfohlene Einstellungen

Verwende diese Verbindungseinstellungen in Bearcat:

```text
Host: localhost
Port: 5432
Database: bearcat
Username: bearcat
Password: choose-a-password
```

Wähle ein eigenes Passwort. Änderst du andere Werte, trage sie auch in Bearcat ein.

## PostgreSQL in Docker

Unter Windows ist Docker die empfohlene Variante für PostgreSQL. Bearcat kann weiterhin als Desktopanwendung oder Windows-Dienst laufen.

Erstelle einen Ordner für die Datenbank:

```bash
mkdir -p ~/Bearcat/postgres-data
```

Das ist der Datenbankordner. Nimm ihn in deine Backups auf.

Starte PostgreSQL 18:

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

In Windows PowerShell verwendest du stattdessen einen Windows-Pfad:

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

Trage die [Verbindungseinstellungen oben](#empfohlene-einstellungen) in Bearcat ein, mit dem Passwort, das du für den Container gewählt hast.

Datenbank stoppen:

```bash
docker stop bearcat-postgres
```

Später wieder starten:

```bash
docker start bearcat-postgres
```

Stoppe den Container, bevor du den Datenordner für ein Backup kopierst. Löschst du diesen Ordner, löschst du die Bearcat-Datenbank.

## Windows

Als Alternative zu [Docker](#postgresql-in-docker) kannst du PostgreSQL als Windows-Dienst installieren:

### Nativer Installer

Lade den Windows-Installer für PostgreSQL 18 von [postgresql.org/download/windows](https://www.postgresql.org/download/windows/) herunter.

Während der Installation:

- Wähle PostgreSQL 18.
- Behalte den Standardport `5432`, sofern er nicht bereits belegt ist.
- Setze ein Passwort für den Superuser `postgres`, den der Installer erstellt.
- Stack Builder ist optional und für Bearcat nicht nötig.

Wähle das Konto, das Bearcat verwenden soll:

- **Installationskonto:** Gib in Bearcat `postgres` und das bei der Installation gewählte Passwort ein. Damit verwendet Bearcat den PostgreSQL-Superuser.
- **Separates Konto:** Öffne **pgAdmin 4**, verbinde dich als `postgres` und erstelle eine Login-Rolle namens `bearcat`. Setze ein Passwort und aktiviere **Can create databases?**. Trage dieses Konto dann in Bearcat ein.

Setze den Datenbanknamen auf `bearcat`. Bearcat legt die Datenbank beim ersten Start an und führt die Migrationen aus.

## macOS

Lade Postgres.app von [postgresapp.com](https://postgresapp.com) herunter.

Installiere und starte Postgres.app, erstelle oder starte dann einen PostgreSQL-18-Server. Behalte den Standardport `5432`, sofern er nicht bereits belegt ist.

Verwende den Befehl `psql` von Postgres.app im Terminal. Findet deine Shell ihn nicht, folge der
Anleitung von Postgres.app: Füge die Kommandozeilentools zu `PATH` hinzu oder verwende den vollständigen Pfad.

Erstelle ein Konto, das die Datenbank anlegen darf:

```bash
psql postgres
```

```sql
CREATE USER bearcat WITH PASSWORD 'choose-a-password';
ALTER USER bearcat CREATEDB;
```

Um die Datenbank stattdessen manuell anzulegen:

```bash
psql postgres
```

```sql
CREATE USER bearcat WITH PASSWORD 'choose-a-password';
CREATE DATABASE bearcat OWNER bearcat;
```

Trage die Verbindungseinstellungen anschliessend in Bearcat ein.

## Fehlerbehebung

Wenn Bearcat keine Verbindung herstellen kann:

- Prüfe, ob PostgreSQL läuft.
- Prüfe, ob der Port in Bearcat mit dem PostgreSQL-Port übereinstimmt (Standard: `5432`).
- Prüfe, ob Benutzername und Passwort übereinstimmen.
- Fehlt die Datenbank, gib dem Benutzer die Berechtigung `CREATEDB` oder lege die Datenbank manuell an.
