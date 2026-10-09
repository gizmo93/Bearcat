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

The Windows service starts Bearcat with Windows and keeps it running when you are not logged in.
New installations use **SQLite**, which needs no database server.
To start and stop Bearcat yourself, use the [Desktop app](/Bearcat/use-the-desktop-launcher/).

## 1. Install the archive tools and download Bearcat

Install [WinRAR](https://www.rarlab.com/download.htm) and [7-Zip](https://www.7-zip.org/download.html).
Create a folder for your releases, for example `C:\Bearcat\releases`.

Download the `Bearcat.Service-win-...zip` package for your computer from
[GitHub releases](https://github.com/gizmo93/Bearcat/releases) and extract it, for example to
`C:\Program Files\Bearcat`. Keep all files in the same folder.

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

**Next: [Add your hoster account and upload your first release](/Bearcat/post-installation/).**

## Change the setup later

Run `.\Bearcat.Cli.exe setup` again to change paths, the port, or the database.
It preselects your current database and keeps the service registration and account.

To use PostgreSQL, [set up a PostgreSQL server](/Bearcat/install-postgresql-for-desktop/) first.
Select **PostgreSQL** during setup and enter its host, port, database name, username, and password.
Setup tests the connection.

Switching databases does not transfer data; the new database starts empty.

## Where the configuration is stored

Setup saves this computer's settings here:

```text
%ProgramData%\Bearcat\config.json
```

The file contains database settings, archive tool paths, working directories, and the web port.
Only the service account and Administrators can access the folder. PostgreSQL passwords are stored in plain text.

Bearcat stores the key for your encrypted credentials in the same folder:

```text
%ProgramData%\Bearcat\bearcat.key
```

Back up `bearcat.key` together with your database:

- SQLite: the database file, by default `%ProgramData%\Bearcat\bearcat.db`. Stop the service before copying the file. While the service runs, use `sqlite3 bearcat.db ".backup bearcat-backup.db"` instead.
- PostgreSQL: your PostgreSQL database.

Without `bearcat.key`, Bearcat cannot decrypt your saved credentials.

`setup` and `set-db-password` keep custom sections such as `Logging` or `Bearcat`.

To enable the REST API command endpoints, add `Bearcat.ApiKey`. See [Control Bearcat from External Tools](/Bearcat/external-orchestration/#windows-service).

## Managing the service

Open **Services** (`services.msc`) and find **Bearcat**.

![Bearcat in Services](images/services-app.png)

Or use an Administrator terminal:

```text
sc.exe start Bearcat
sc.exe stop Bearcat
sc.exe query Bearcat
```

The service restarts automatically after a crash.

## Checking the logs

The service logs to the Windows **Event Log**. Open the **Event Viewer**, go to **Windows Logs => Application**, and filter by the source **Bearcat**.

![Bearcat entries in Event Viewer](images/windows-event-viewer.png)

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

Restart the service to apply the change:

```text
sc.exe stop Bearcat
sc.exe start Bearcat
```

## Using a network share for releases

The service runs as `LocalSystem` by default. This account cannot access protected network shares.
Mapped drives such as `Z:` are not visible to services. If your release data lives on a network share:

1. Enter it as a **UNC path** (`\\server\share\releases`) during setup, not a mapped drive letter.
2. Open `services.msc` => **Bearcat** => **Log On**, and set an account that has access to the share.

   ![Service login account](images/windows-service-user.png)

3. Restart the service.

The setup reminds you about this when it detects a UNC path. The account is kept when you run `setup` again.

## Changing the database password

For PostgreSQL, update the database password with this command as Administrator:

```text
.\Bearcat.Cli.exe set-db-password
```

It prompts for the new password, tests the connection, updates the configuration, and restarts the service.
With SQLite, it exits with an error and leaves the configuration unchanged.

## Updating

Back up your database before a major update. Bearcat applies database migrations on startup.

1. Stop the service with `sc.exe stop Bearcat` to unlock the application files.
2. Replace all files in the install folder with the new release, including the DLLs.
3. Start the service with `sc.exe start Bearcat`.

Settings, `bearcat.key`, and the default SQLite database stay in `%ProgramData%\Bearcat`.
The service registration and account are kept.

## Uninstalling

To stop and remove the service, run (as Administrator):

```text
.\Bearcat.Cli.exe uninstall
```

The command removes the service and offers to delete `config.json`. The database is kept.
