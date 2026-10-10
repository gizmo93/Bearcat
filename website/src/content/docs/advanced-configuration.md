---
title: "Background Tasks and Advanced Settings"
description: "Configure background tasks, cleanup rules, upload limits, the database, and the time zone."
---

Bearcat packs, uploads, and checks files in the background. Manage tasks under **Background tasks**
and their settings under **Configurations**.

## Background tasks

Each task runs on its own schedule.

![background-tasks-page.png](images/background-tasks-page.png)

The table shows each task's status, start and finish times, duration, and last error.
Use **Active** to enable or disable future runs. Running tasks are not canceled.
All tasks are enabled by default.

### Available background tasks

| Task | Runs | What it does |
| --- | ---: | --- |
| Configuration cache refresh | Every 5 minutes | Refreshes cached settings from the database. Settings saved in the UI take effect immediately. |
| Release folder automation | Every 2 minutes | Creates releases from matching direct subfolders using the selected template, once the [folder checks](#folder-automation) pass. |
| Remote source scan | Every 2 minutes | Finds matching folders on FTP or FTPS servers and queues them once the [remote folder checks](#remote-downloads) pass. |
| Remote source download | Every 20 seconds | Downloads queued remote folders. |
| Remote download verification and extraction | Every 20 seconds | Checks completed downloads against their SFV files and extracts archives when the automation is set to do so. |
| Remote download release creation | Every 20 seconds | Creates releases from completed downloads using their selected templates. |
| Remote download raw file cleanup | Every 2 minutes | Converts downloaded releases to unmanaged and removes their raw files when [Keep raw files](/Bearcat/remote-downloads/#delete-raw-files-after-uploading) is off and the cleanup conditions are met. |
| Release info resolution | Every 10 minutes | Fills in missing scene information, NFOs, external IDs, and metadata from active sources. |
| Archive creation & restore | Every 20 seconds | Provides archives for waiting uploads: reuses existing archives, restores missing files from a mirror, or creates new archives. |
| Auto cleanup | Every 30 minutes | Applies enabled [cleanup rules](#auto-cleanup) after their retention periods. |
| Archive upload | Every 20 seconds | Uploads archives to the configured hosters. |
| Image upload | Every 30 seconds | Uploads release cover images to configured image hosters when a cover image URL is available. |
| Upload state check | Every 20 seconds | Checks whether uploaded files are still online, creates initial upload records after the configured cooldown and schedules automatic reuploads when release group rules allow it. |
| Link crypter container creation | Every 20 seconds | Creates link crypter containers for completed uploads. |

## Configurations

Open **Configurations** in the sidebar to change global settings.

Switches and dropdowns save immediately. Number fields have save and discard buttons.
You can also press Enter to save or Escape to discard.

Changed settings have a dot beside their name and show the default value.
Click **Reset** to restore the default.

### Auto cleanup

**Auto cleanup** frees disk space. It has two independent rules, both disabled by default:

- "Convert releases to unmanaged automatically" with "Convert to unmanaged after", default `14` days.
- "Delete local archives automatically" with "Delete local archives after", default `30` days.

![auto-cleanup-config.png](images/auto-cleanup-config.png)

These rules do not delete your release folder. Delete it yourself once you no longer need it.
[Remote downloads](/Bearcat/remote-downloads/#delete-raw-files-after-uploading) have a separate option
to delete downloaded raw files after uploading.

#### Convert releases to unmanaged automatically

This rule converts managed releases to unmanaged. Bearcat then reuploads existing archives instead of using the original release files to create new ones.
The retention period starts at the posting date, or the first completed upload if the release was
never marked as posted. Without either date, the release stays managed.

When the period has passed, Bearcat checks that:

- No upload of the release is running.
- Every archive configuration has a local archive, or an online mirror on a hoster that is enabled for [mirror downloads](/Bearcat/mirror-downloads/).

Once both conditions are met, Bearcat converts the release to unmanaged and removes the stored release
folder path. A notification includes this path so you can delete the folder yourself.

If any archive is stored inside the release folder, the notification says so and asks you to delete only the release data and keep the archive files.

#### Delete local archives automatically

This rule deletes the local archive files of an archive configuration.
The clock starts at the last finished upload of that archive configuration, so every reupload restarts the period.

Bearcat only deletes when the archives stay recoverable:

- A fully online upload exists on a hoster that is enabled for [mirror downloads](/Bearcat/mirror-downloads/), or
- the release is managed and still has a release folder to repack from.

Bearcat removes the archive folder if it is empty afterwards. Files on the hoster are kept.

#### Exclude single releases

Enable **Exclude from auto cleanup** when creating or editing a release to skip both rules.

![exclude-from-auto-cleanup.png](images/exclude-from-auto-cleanup.png)

For remote downloads, also leave
**Keep raw files** enabled in the automation to keep the downloaded folder.

### Archive repackaging

Hosters can recognise previously uploaded files by their MD5 hash. Bearcat changes the hashes
before uploading an archive to the same hoster type again. How it does this depends on the format:

- **RAR:** Bearcat keeps the hash history for each archive configuration, even after local archives
  are deleted. It appends zero bytes to each newly packed part and to each reused part that is
  uploaded again until its hash is new.
  The archive still extracts normally. The configured part size stays the same; the default
  `1` MB increase is normally skipped. Compression and solid mode still follow the selected strategy.
- **7-Zip:** Bearcat must repack the release to change the hashes. Choose a **Repackaging strategy**
  under **Configurations**:

| Strategy | Effect when packing 7-Zip archives |
| --- | --- |
| **Change archive file size by 1 MB** (default) | No compression or solid mode. Each new archive uses a part size `1` MB larger than the previous one in the same archive configuration. The first uses the configured size. |
| **Nonce only, no compression** | Only the random `__nonce.txt` changes. Uses little CPU, but some parts may keep their old hash. |
| **Solid archive with compression** | Compression in solid mode spreads the nonce change more reliably across the parts, but uses more CPU. |

**Create nonce file** is on by default in the archive configuration. If you turn it off,
7-Zip always increases the part size by `1` MB, regardless of the strategy. RAR still gets new hashes
from appended zero bytes.

If the previous RAR archive has missing hashes, for example from before hash tracking was added,
the selected strategy also controls the part size when repacking. With the default strategy,
that archive uses the previous part size plus `1` MB.

### Folder automation

Bearcat creates a release from a watched folder once both conditions are met:

- Its total file count and size stay unchanged for "Folder stability", default `5` minutes.
- It reaches "Minimum folder size", default `1` MB.

This helps avoid creating releases while files are still being copied.

Increase the stability period for slow or interrupted copies. Set the minimum size to `0` to allow empty folders.
Manual release creation and metadata lookup under **Release info** are unaffected.

### Remote downloads

Find settings for [FTP / FTPS downloads](/Bearcat/remote-downloads/) under **Configurations > Remote downloads**.
They apply independently of local folder automation.

| Setting | Default | Effect |
| --- | --- | --- |
| Folder stability | `5` min | Wait until the remote folder's file count and total size stay unchanged for this long. |
| Minimum folder size | `1` MB | Keep smaller folders in **Observing**. Set to `0` to disable the size check. |
| Maximum parallel file downloads | `4` | Limit how many files are downloaded at the same time. The source's **Max connections** also applies. |
| Download retry delay | `30` s | Wait this long before retrying a failed download attempt. |

Increase the stability period if files arrive slowly or uploads to the FTP server often pause.
Bearcat downloads one release folder at a time, with several files in parallel. Lower the connection
limit if the server rejects simultaneous transfers.

### Initial upload cooldown

"Initial upload cooldown" defaults to `5` minutes. It gives you time to finish setting up a release before archiving and uploading begin.

Once an upload configuration exists and the release is older than the cooldown, the next "Upload state check" run creates the first upload record.
The wait is measured from release creation. Set it to `0` to allow the first upload on the next check.

Changes only affect upload configurations without an upload. Existing uploads are not canceled or delayed.

### Upload concurrency

"Maximum parallel uploads" defaults to `10`. It limits parallel file uploads across all hosters.
Each hoster's own limit also applies.

View and, where allowed, override individual limits on the "Hoster registrations" page. See [Parallel uploads per hoster](/Bearcat/account-settings/#parallel-uploads-per-hoster).

"Maximum upload speed" (MB/s) is empty by default, which means no limit. It caps the combined upload
speed of all running uploads across all hosters. Decimal values such as `0.5` or `0,5` are allowed.
Each hoster registration can set its own limit as well. See
[Upload speed limit per hoster](/Bearcat/account-settings/#upload-speed-limit-per-hoster).

Very low speed limits can cause uploads to time out.

## Database

New Desktop, Windows service, and Docker installations use SQLite. Existing PostgreSQL setups keep using PostgreSQL.

Database settings are read at startup from environment variables or a settings file:

| Installation | Where to set them |
| --- | --- |
| Docker | `docker-compose.yml`, or `docker-compose.postgres.yml` for PostgreSQL. See [Run Bearcat in Docker](/Bearcat/use-the-docker-image/#database). |
| Desktop app | **Database type** in the [Desktop app](/Bearcat/use-the-desktop-launcher/#database-type) settings. |
| Windows service | `Database` section in `%ProgramData%\Bearcat\config.json`, written by `Bearcat.Cli.exe setup`. |

| Setting | Environment variable | Value |
| --- | --- | --- |
| `Database:Provider` | `Database__Provider` | `Sqlite` or `Postgres`, case-insensitive. Not set: `Postgres`. |
| `Database:SqliteFilePath` | `Database__SqliteFilePath` | Path to the SQLite database file. Not set: `bearcat.db` in the data directory. |
| `Database:ConnectionString` | `Database__ConnectionString` | PostgreSQL connection string, for example `Host=localhost;Database=bearcat;Username=bearcat;Password=...`. Only used with `Postgres`. |

Bearcat checks these locations in order to find its data directory:

1. Environment variable `BEARCAT_DATA_DIR`
2. Setting `Bearcat:DataDirectory` (environment variable `Bearcat__DataDirectory`)
3. `/data` inside a container
4. The user's application data folder plus `Bearcat`: `%APPDATA%\Bearcat` on Windows, `~/Library/Application Support/Bearcat` on macOS

The Windows service uses `%ProgramData%\Bearcat` as its data directory.

SQLite notes:

- Keep the SQLite database on local storage. Network shares (NFS/SMB) are unsuitable for its WAL mode.
- Back up `bearcat.key` from the data directory together with the database file. Stop Bearcat before copying the database file. Copying the database while Bearcat runs may produce an inconsistent backup, even with the `-wal` and `-shm` files. Use `sqlite3 bearcat.db ".backup bearcat-backup.db"`.

Changing `Database:Provider` does not transfer existing data. A new database starts empty.
Without `Database:Provider`, Bearcat uses PostgreSQL so older configurations keep working.

## Time zone

Bearcat uses the local time zone for displayed times and time-based rules.
By default, it uses the operating system's time zone.

| Installation | Default | How to change it |
| --- | --- | --- |
| Desktop app | Time zone of your computer | Change the time zone of your computer. |
| Windows service | Time zone of the Windows server | Add `"LocalTimezone": "Europe/Berlin"` to `%ProgramData%\Bearcat\config.json` and restart the service. |
| Docker | UTC | Set `BEARCAT_TIMEZONE` in `.env`. See [Docker settings](/Bearcat/use-the-docker-image/#docker-settings). |

The value is an IANA time zone ID such as `Europe/Berlin` or `America/New_York`.
An unknown ID stops Bearcat on startup.
