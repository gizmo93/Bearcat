---
title: "Install Bearcat on Windows and macOS"
description: "Install the desktop app, choose your release folder, and start Bearcat with SQLite."
prev:
  label: "Choose an installation"
  link: "/Bearcat/#get-started"
next:
  label: "Your first upload"
  link: "/Bearcat/post-installation/"
---

The Desktop app starts Bearcat on your computer and opens its web interface in your browser.
New installations use **SQLite**, which needs no separate database installation.

## 1. Install the archive tools

Bearcat needs both RAR and 7-Zip to start.

| System | What to install |
| --- | --- |
| Windows | [WinRAR](https://www.rarlab.com/download.htm) and [7-Zip](https://www.7-zip.org/download.html). |
| macOS | [RAR for macOS ARM](https://www.rarlab.com/download.htm) and the [7-Zip console version for macOS](https://www.7-zip.org/download.html). Extract both into folders you will keep. |

On Windows, the usual executable paths are `C:\Program Files\WinRAR\Rar.exe` and
`C:\Program Files\7-Zip\7z.exe`. On macOS, select the extracted `rar` and `7zz` files in step 3.
If you already installed the tools another way, use their executable paths.

## 2. Download and open Bearcat

Download the Desktop package for your computer from [GitHub releases](https://github.com/gizmo93/Bearcat/releases):

- **Windows:** extract the `Bearcat.Desktop-win-...zip` package and open `Bearcat.Desktop.exe`. Keep the extracted files together.
- **macOS (Apple Silicon):** extract `Bearcat.Desktop-macos-arm64.zip`, move **Bearcat Desktop.app** to **Applications**, and open it.

<details>
<summary>macOS says the app is damaged or blocks it from opening</summary>

The app is ad-hoc signed. If macOS blocks your GitHub download, move the app to `/Applications`,
then open Terminal and run:

```bash
xattr -dr com.apple.quarantine "/Applications/Bearcat Desktop.app"
```

Open the app again.

</details>

## 3. Choose your folders and tools

Create a folder for your releases, for example `C:\Bearcat\releases` on Windows or
`~/Bearcat/releases` on macOS. In the Desktop app, fill in:

| Field | What to enter |
| --- | --- |
| **Working directories** | Click **Add...** and select your release folder. Bearcat must be able to read and write here. |
| **RAR executable** | Click **Browse...** and select `Rar.exe` on Windows or `rar` on macOS. |
| **7z executable** | Click **Browse...** and select `7z.exe` on Windows or `7zz` on macOS. |

Keep **SQLite (recommended)** selected and leave **Database file** at its default.
You can also leave **Bearcat Host**, **Web port**, and **API key** unchanged.

## 4. Start Bearcat

Click **Save**, then **Start Bearcat**. Once it is ready, click **Open Bearcat** or open
[http://127.0.0.1:17208](http://127.0.0.1:17208).

**Next: [Add your hoster account and upload your first release](/Bearcat/post-installation/).**

Closing the settings window keeps Bearcat running. Use **Stop** or **Quit** to stop it.
For Windows installations that should run even when nobody is logged in, use the [Windows service](/Bearcat/use-the-windows-service/).

## Other settings

Change these only when you need them.

### Database type

SQLite stores your data in a file on this computer. Keep it on a local drive, not a network share.
The default location is:

```text
Windows: %APPDATA%\Bearcat\bearcat.db
macOS: ~/Library/Application Support/Bearcat/bearcat.db
```

To use PostgreSQL, [set up a PostgreSQL server](/Bearcat/install-postgresql-for-desktop/), select
**PostgreSQL**, and enter its host, port, database name, username, and password.
Bearcat updates the database on startup and can create a PostgreSQL database if the user has permission.

Older installations keep their PostgreSQL settings. Changing the database type does not transfer
existing data. A new database starts empty.

### Bearcat Host

Leave this empty for automatic detection. If detection fails, select the `Bearcat.Host` executable
or `Bearcat.Host.dll` from the downloaded package.

### Web port

Keep `17208` unless another application uses that port. If you change it, use the new port in the browser address.

### API key

Leave this empty unless you want to [control Bearcat through the REST API](/Bearcat/external-orchestration/#api-key).

## Settings and backups

The launcher saves its settings here:

```text
Windows: %APPDATA%\Bearcat\Desktop\settings.json
macOS: ~/Library/Application Support/Bearcat/Desktop/settings.json
```

This file includes the API key and PostgreSQL password if you configured them.

Bearcat also creates `bearcat.key` in `%APPDATA%\Bearcat` on Windows or
`~/Library/Application Support/Bearcat` on macOS. It uses this key to read your saved account credentials.

For a SQLite backup, stop Bearcat and copy both `bearcat.db` and `bearcat.key`.
While Bearcat runs, use `sqlite3 bearcat.db ".backup bearcat-backup.db"` instead of copying `bearcat.db`.
If you changed the database file location, copy it from there. With PostgreSQL, back up that database and `bearcat.key`.
Keep both when moving to another computer. Without the key, Bearcat cannot read your saved account credentials.

## If Bearcat does not start

- **RAR or 7z was not found:** select the executable files with **Browse...**. On macOS, the official 7-Zip download names its executable `7zz`.
- **A folder is missing or not writable:** check your working directories and database file location.
- **The web port is in use:** choose another port and start Bearcat again.
- **Changes seem to have no effect on Windows:** check the tray for another running copy of Bearcat Desktop and quit it.
