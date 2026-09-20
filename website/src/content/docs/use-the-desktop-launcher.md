---
title: "Install Bearcat on Windows and macOS"
description: "Run Bearcat locally on Windows or macOS with the desktop launcher."
---

Bearcat Desktop starts Bearcat and opens it in your browser. Use the tray menu to start and stop it, or quit the app to stop Bearcat.

On Windows, use the Desktop app for on-demand use or the [Windows service](/Bearcat/use-the-windows-service/) to keep Bearcat running continuously. Both run the same application.
On Apple Silicon Macs, I recommend the Desktop app because it runs natively on ARM.

Install PostgreSQL separately before you start.

| macOS | Windows |
| --- | --- |
| ![Bearcat Desktop on macOS](images/desktop-mac.png) | ![Bearcat Desktop on Windows](images/desktop-windows.png) |

## Requirements

- PostgreSQL 18 running locally or on a reachable machine
- RAR command line executable (on Windows this comes with WinRAR)
- 7z command line executable
- A release data directory on your local machine

For PostgreSQL setup instructions, see [Set Up PostgreSQL for Bearcat](/Bearcat/install-postgresql-for-desktop/).

## Downloading the newest release
You can get the latest release of the Desktop app from the [GitHub releases page](https://github.com/gizmo93/Bearcat/releases).
![download-desktop-app.png](images/download-desktop-app.png)


### macOS Gatekeeper

The macOS app is ad-hoc signed because I don't have an Apple Developer License. macOS may quarantine the download and block it from opening.
If macOS reports that the app is damaged after downloading it from GitHub, move the app to `/Applications`, open the Terminal and remove the quarantine attribute:

```bash
xattr -dr com.apple.quarantine "/Applications/Bearcat Desktop.app"
```

After that, open the app again.

## Settings

On first start, open the launcher settings and enter:

### Release path

The folder where Bearcat looks for release files. This should be a local folder, mounted drive, or network share that the desktop user can read and write.

### RAR executable

Path to the RAR command line executable. You can enter a full path, or a command name if it is available on `PATH`.

Examples:

```text
rar
C:\Program Files\WinRAR\Rar.exe
/usr/local/bin/rar
```

### 7z executable

Path to the 7-Zip command line executable. You can enter a full path, or a command name if it is available on `PATH`.

Examples:

```text
7z
C:\Program Files\7-Zip\7z.exe
/opt/homebrew/bin/7z
```

### Bearcat Host

Leave this empty to let the launcher find `Bearcat.Host` automatically. If detection fails, select the published executable or `Bearcat.Host.dll`.

For development builds, the launcher can also find the host in the local repository.

### PostgreSQL settings

The Desktop app uses your own PostgreSQL server. Enter:

- Host: PostgreSQL server hostname, usually `localhost` for a local database.
- Port: PostgreSQL port, usually `5432`.
- Database: Bearcat database name, for example `bearcat`.
- Username: PostgreSQL user.
- Password: PostgreSQL password.

Bearcat updates the database schema on startup. It can also create the database if the PostgreSQL user has permission.

### Web port

The local HTTP port for the Bearcat web UI. The default is `17208`, so the app opens:

```text
http://127.0.0.1:17208
```

Change this only if the port is already used by another application.

## Where Settings Are Stored

Settings are stored as JSON in the user's application data directory:

```text
Windows: %APPDATA%\Bearcat\Desktop\settings.json
macOS: ~/Library/Application Support/Bearcat/Desktop/settings.json
```

The file contains the Desktop app settings, including the PostgreSQL password. Protect your operating system user account accordingly.

## Where The Encryption Key Is Stored

Bearcat encrypts stored hoster, link crypter, and NFO database account configurations.
The Desktop app creates the encryption key automatically on first start.

Default key locations:

```text
Windows: %APPDATA%\Bearcat\bearcat.key
macOS: ~/Library/Application Support/Bearcat/bearcat.key
```

Back up this file together with your PostgreSQL database.
If you move the Desktop setup to another computer, copy both the database and `bearcat.key`.
Without `bearcat.key`, Bearcat cannot decrypt stored account configurations.

## Starting Bearcat

Choose `Start Bearcat` in the app window or tray menu. Once Bearcat is ready, you can open it at:

```text
http://127.0.0.1:<web-port>
```

Closing the settings window hides it. Bearcat keeps running until you choose `Stop` or `Quit Bearcat`.

Attention: On Windows, multiple copies of the app can be started at the same time. If a setting or update appears not to take effect, check the tray area and quit old Bearcat Desktop instances.
