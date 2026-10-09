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

The Desktop app starts Bearcat and opens its web interface in your browser.
New installations use **SQLite**, which needs no database server.

## 1. Install the archive tools

Bearcat needs both RAR and 7-Zip to start.

| System | What to install |
| --- | --- |
| Windows | [WinRAR](https://www.rarlab.com/download.htm) and [7-Zip](https://www.7-zip.org/download.html). |
| macOS | [RAR for macOS ARM](https://www.rarlab.com/download.htm) and the [7-Zip console version for macOS](https://www.7-zip.org/download.html). Extract both into folders you will keep. |

In step 3, select the tools’ executable files. On Windows, these are usually
`C:\Program Files\WinRAR\Rar.exe` and `C:\Program Files\7-Zip\7z.exe`.
On macOS, select the extracted `rar` and `7zz` files.

On macOS, you can also install [7-Zip via Homebrew](https://formulae.brew.sh/formula/sevenzip):

```bash
brew install sevenzip
```

For Homebrew, select `/opt/homebrew/bin/7zz` under **7z executable** in step 3
(the default path on Apple Silicon).

[RAR's Homebrew cask](https://formulae.brew.sh/cask/rar) (`brew install --cask rar`) is disabled.
Use the official download above. If RAR is already installed through Homebrew,
select `/opt/homebrew/bin/rar` under **RAR executable**.

## 2. Download and open Bearcat

Download the Desktop package for your computer from [GitHub releases](https://github.com/gizmo93/Bearcat/releases):

- **Windows:** extract the `Bearcat.Desktop-win-...zip` package and open `Bearcat.Desktop.exe`. Keep all extracted files in the same folder.
- **macOS (Apple Silicon):** extract `Bearcat.Desktop-macos-arm64.zip`, move **Bearcat Desktop.app** to **Applications**, and open it.

<details>
<summary>macOS says the app is damaged or blocks it from opening</summary>

If macOS blocks the app, move it to `/Applications` and run this in Terminal:

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

Keep the defaults for **SQLite (recommended)**, **Database file**, **Bearcat Host**, **Web port**, and **API key**.

## 4. Start Bearcat

Click **Save**, then **Start Bearcat**. Once it is ready, click **Open Bearcat** or open
[http://127.0.0.1:17208](http://127.0.0.1:17208).

**Next: [Add your hoster account and upload your first release](/Bearcat/post-installation/).**

Closing the settings window keeps Bearcat running. Use **Stop** or **Quit** to stop it.
To run Bearcat on Windows without logging in, use the [Windows service](/Bearcat/use-the-windows-service/).

## Other settings

### Database type

SQLite stores your data in a file on this computer. Keep it on a local drive, not a network share.
The default location is:

```text
Windows: %APPDATA%\Bearcat\bearcat.db
macOS: ~/Library/Application Support/Bearcat/bearcat.db
```

To use PostgreSQL, [set up a PostgreSQL server](/Bearcat/install-postgresql-for-desktop/), select
**PostgreSQL**, and enter its host, port, database name, username, and password.
Bearcat updates the database on startup and creates it if the user has permission.

Existing PostgreSQL settings are kept. Switching databases does not transfer data; the new database starts empty.

### Bearcat Host

Leave this empty for automatic detection. If detection fails, select the `Bearcat.Host` executable
or `Bearcat.Host.dll` from the downloaded package.

### Web port

Keep `17208` unless another application uses that port. If you change it, use the new port in the browser address.

### API key

Leave this empty unless you want to [control Bearcat through the REST API](/Bearcat/external-orchestration/#api-key).

## Settings and backups

The Desktop app saves its settings here:

```text
Windows: %APPDATA%\Bearcat\Desktop\settings.json
macOS: ~/Library/Application Support/Bearcat/Desktop/settings.json
```

It also contains any API key and PostgreSQL password you entered.

Bearcat also creates `bearcat.key` in `%APPDATA%\Bearcat` on Windows or
`~/Library/Application Support/Bearcat` on macOS. It uses this key to decrypt your saved credentials.

For a backup, keep the database and `bearcat.key` together. Without the key, Bearcat cannot decrypt
your credentials after a restore or a move to another computer.

- **SQLite:** stop Bearcat and copy `bearcat.db` from its configured location. While Bearcat runs, use `sqlite3 bearcat.db ".backup bearcat-backup.db"` instead of copying the database file.
- **PostgreSQL:** back up the PostgreSQL database.

Copy `bearcat.key` with either backup.

## If Bearcat does not start

- **RAR or 7z was not found:** select the executable files with **Browse...**. On macOS, the official 7-Zip download names its executable `7zz`.
- **A folder is missing or not writable:** check your working directories and database file location.
- **The web port is in use:** choose another port and start Bearcat again.
- **Changes seem to have no effect on Windows:** check the tray for another running copy of Bearcat Desktop and quit it.
