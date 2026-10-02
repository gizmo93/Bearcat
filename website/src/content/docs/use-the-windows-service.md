---
title: "Run Bearcat as a Windows Service"
description: "Start Bearcat automatically with Windows, even when nobody is logged in."
prev:
  label: "Choose an installation"
  link: "/Bearcat/#get-started"
next:
  label: "Your first upload"
  link: "/Bearcat/post-installation/"
---

Use the Windows service to keep Bearcat running when you are not logged in.
New installations use **SQLite**, which needs no database server.
For an app you start and stop yourself, use the [Desktop app](/Bearcat/use-the-desktop-launcher/).

## 1. Install the archive tools and download Bearcat

Install [WinRAR](https://www.rarlab.com/download.htm) and [7-Zip](https://www.7-zip.org/download.html).
Create a folder for your releases, for example `C:\Bearcat\releases`.

Download the `Bearcat.Service-win-...zip` package for your computer from
[GitHub releases](https://github.com/gizmo93/Bearcat/releases) and extract it, for example to
`C:\Program Files\Bearcat`. Keep all files together.

## 2. Run setup

Open PowerShell **as Administrator**, change into the extracted folder, and run:

```powershell
cd "C:\Program Files\Bearcat"
.\Bearcat.Cli.exe setup
```

Use these answers for a new installation:

| Prompt | What to enter |
| --- | --- |
| **Database** | Keep **SQLite (recommended)**. |
| **SQLite database file** | Keep the default, `%ProgramData%\Bearcat\bearcat.db`. |
| **Path to 7z executable** | Usually `C:\Program Files\7-Zip\7z.exe`. |
| **Path to rar executable** | Usually `C:\Program Files\WinRAR\Rar.exe`. |
| **Working directory** | Your release folder, for example `C:\Bearcat\releases`. |
| **Additional working directory** | Leave empty to continue. |
| **Web port** | Keep `17208`. |

If you installed the archive tools elsewhere, enter their actual paths.
Keep the SQLite database on a local drive. For releases on a network share, see
[Using a network share](#using-a-network-share-for-releases).

Setup saves the configuration, installs the service, and starts Bearcat.

## 3. Open Bearcat

Open [http://127.0.0.1:17208](http://127.0.0.1:17208), or use the port you chose during setup.
Bearcat starts automatically with Windows from now on.

**Next: [Add your hoster account and upload your first release](/Bearcat/post-installation/).**

## Change the setup later

Run `.\Bearcat.Cli.exe setup` again to change paths, the port, or the database.
It keeps the existing service registration and service account. For existing installations,
the database prompt preselects the database already in use.

To use PostgreSQL, [set up a PostgreSQL server](/Bearcat/install-postgresql-for-desktop/) first.
Select **PostgreSQL** during setup and enter its host, port, database name, username, and password.
Setup tests the connection.

Changing the database type does not transfer existing data. A new database starts empty.

## Where the configuration is stored

The setup writes a single machine-wide configuration file:

```text
%ProgramData%\Bearcat\config.json
```

It holds the database settings, the 7z/RAR paths, the working directories, and the web port. The folder's access is restricted to the service account and Administrators. With PostgreSQL, the file contains the database password in plain text.

The encryption key for stored hoster, link crypter, and NFO database account configurations is created next to it on first start:

```text
%ProgramData%\Bearcat\bearcat.key
```

Back up `bearcat.key` together with your database:

- SQLite: the database file, by default `%ProgramData%\Bearcat\bearcat.db`. Stop the service before copying, or copy `bearcat.db-wal` and `bearcat.db-shm` together with `bearcat.db`.
- PostgreSQL: your PostgreSQL database.

Without `bearcat.key`, Bearcat cannot decrypt your stored account configurations anymore.

`setup` and `set-db-password` only update these values. Sections you add by hand, like `Logging` or `Bearcat`, are kept.

To enable the REST API command endpoints, add `Bearcat.ApiKey`. See [Orchestrate Bearcat from External Tools](/Bearcat/external-orchestration/#windows-service).

## Managing the service

The service is registered under the name **Bearcat**. You can manage it like any other Windows service:

- Through the **Services** app (`services.msc`)
![services-app.png](images/services-app.png)

- Or from an Administrator terminal:

```text
sc.exe start Bearcat
sc.exe stop Bearcat
sc.exe query Bearcat
```

It is set to start automatically and to restart itself if it crashes.

## Checking the logs

The service logs to the Windows **Event Log**. Open the **Event Viewer**, go to **Windows Logs => Application**, and filter by the source **Bearcat**.

![windows-event-viewer.png](images/windows-event-viewer.png)

## More detailed logs

Bearcat logs warnings and errors by default. For more detail, add the following `Logging` section to `config.json`. Keep your existing connection, paths, and port values:

```json
{
  "Database": { "Provider": "Sqlite", "SqliteFilePath": "..." },
  "Archivers": { "RarPath": "...", "SevenZipPath": "..." },
  "WorkingDirectories": ["..."],
  "Urls": "http://127.0.0.1:17208",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Bearcat": "Debug"
    }
  }
}
```

Then restart the service so it picks up the change:

```text
sc.exe stop Bearcat
sc.exe start Bearcat
```

## Using a network share for releases

The service runs as `LocalSystem` by default, which cannot reach a protected network share, and mapped drive letters (like `Z:`) are not visible to services at all. If your release data lives on a network share:

1. Enter it as a **UNC path** (`\\server\share\releases`) during setup, not a mapped drive letter.
2. Open `services.msc` => **Bearcat** => **Log On**, and set an account that has access to the share.
![windows-service-user.png](images/windows-service-user.png)
3. Restart the service.

The setup reminds you about this when it detects a UNC path. The account you set here is preserved when you re-run `setup`.

## Changing the database password

PostgreSQL only. If your PostgreSQL password changes, you don't need to edit `config.json` by hand. Run (as Administrator):

```text
.\Bearcat.Cli.exe set-db-password
```

It prompts for the new password, tests the connection, updates the configuration, and restarts the service.
With SQLite, it exits with an error and leaves the configuration unchanged.

## Updating

To update Bearcat, replace the application files. Your configuration and encryption key are stored outside the install folder:

1. Stop the service: `sc.exe stop Bearcat` (the running `.exe` and its DLLs are locked while it runs).
2. Replace the contents of the install folder with the new release. Replace the whole folder, not just `Bearcat.Host.exe`, so all files match the new version.
3. Start the service: `sc.exe start Bearcat`.

`config.json`, `bearcat.key`, and the SQLite database in `%ProgramData%\Bearcat` are untouched, and the service registration and run-as account stay as they were. Database migrations run automatically on start, so back up your database before a major update.

## Uninstalling

To stop and remove the service, run (as Administrator):

```text
.\Bearcat.Cli.exe uninstall
```

It removes the Windows service and offers to delete `config.json`. Your database (SQLite file or PostgreSQL) is never touched.
