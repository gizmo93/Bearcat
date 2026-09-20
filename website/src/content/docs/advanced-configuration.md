---
title: "Configure Background Tasks and Upload Settings"
description: "Tune background tasks, upload checks, and global Bearcat behavior."
---

Bearcat does most work in background tasks.
The web UI gives you two places to control that behavior:

- "Background tasks" shows scheduled work, health and enable / disable switches.
- "Configurations" contains global settings that affect background work.

## Background tasks

Open "Background tasks" in the sidebar to see the current task list.
Each task runs on its own schedule.

![background-tasks-page.png](images/background-tasks-page.png)

The table columns mean:

- "Name" is the task display name.
- "Active" enables or disables future executions of that task.
- "Status" shows "Never run", "Running", "Success" or "Error".
- "Started" and "Finished" show the timestamps of the latest run.
- "Duration" shows how long the latest finished run took.
- "Last error" shows the latest error message if the task failed.

Tasks are enabled by default. Disable one to pause future runs, for example during maintenance. Work already running continues.
Use "Refresh" to reload the latest state.

### Available background tasks

| Task | Runs | What it does |
| --- | ---: | --- |
| Configuration cache refresh | Every 5 minutes | Refreshes cached settings from the database. Settings saved in the UI take effect immediately. |
| Release folder automation | Every 2 minutes | Creates releases from matching direct subfolders using the selected template, once the [folder checks](#folder-automation) pass. |
| Release info resolution | Every 10 minutes | Resolves missing scene release information, NFO files, external IDs, and movie or TV metadata through the active NFO databases and metadata sources. |
| Archive creation & restore | Every 20 seconds | Provides archives for waiting uploads: reuses existing archives, restores missing files from a mirror, or creates new archives. |
| Auto cleanup | Every 30 minutes | Applies enabled [cleanup rules](#auto-cleanup) after their retention periods. |
| Archive upload | Every 20 seconds | Uploads pending archives to the configured hosters and records progress and results. |
| Image upload | Every 30 seconds | Uploads release cover images to configured image hosters when a cover image URL is available. |
| Upload state check | Every 20 seconds | Checks whether uploaded files are still online, creates initial upload records after the configured cooldown and schedules automatic reuploads when release group rules allow it. |
| Link crypter container creation | Every 20 seconds | Creates missing link crypter containers for completed uploads that have link crypter configurations. |

## Configurations

Open "Configurations" in the sidebar to change global application behavior.
Each configuration property shows its current value and its default value.

When you change a value, Bearcat stores it as an override.
Overridden values show an "Override" badge.
Use the reset button next to a property to remove the override and return to the default.

Configuration changes are stored in the database.
They survive container restarts.

### Auto cleanup

"Auto cleanup" frees disk space once a release has been online for a while.
It has two independent rules, and both are disabled by default:

- "Convert releases to unmanaged automatically" with "Convert to unmanaged after (days)", default `14`.
- "Delete local archives automatically" with "Delete local archives after (days)", default `30`.

You can enable either one on its own.
The "Auto cleanup" background task runs the release folder rule first and the archive rule second, so a release gets converted to unmanaged before its archives are considered.

![auto-cleanup-config.png](images/auto-cleanup-config.png)

Bearcat never deletes your release folder. Delete it yourself once you no longer need it.

#### Convert releases to unmanaged automatically

This rule converts managed releases to unmanaged. Bearcat then reuploads existing archives instead of using the original release files to create new ones.
The clock starts at the date the uploads were posted, or at the first finished upload when the release was never marked as posted.
A release with neither is never converted.

When the period has passed, Bearcat checks that:

- No upload of the release is running.
- Every archive configuration has a local archive, or an online mirror on a hoster that is enabled for [mirror downloads](/Bearcat/mirror-downloads/).

If that is the case, Bearcat switches the release to unmanaged, forgets the release folder path, and creates a notification.
The notification contains the release folder path, because after the conversion Bearcat no longer stores it.
Delete the folder yourself if you want.

If any archive is stored inside the release folder, the notification says so and asks you to delete only the release data and keep the archive files.

#### Delete local archives automatically

This rule deletes the local archive files of an archive configuration.
The clock starts at the last finished upload of that archive configuration, so every reupload restarts the period.

Bearcat only deletes when the archives stay recoverable:

- A fully online upload exists on a hoster that is enabled for [mirror downloads](/Bearcat/mirror-downloads/), or
- the release is managed and still has a release folder to repack from.

It deletes the archive files one by one and removes the archive folder only when it is empty afterwards.
After that the archive is marked as deleted in the database.

Uploads on the hoster are never touched.

#### Exclude single releases

Both rules skip releases with the "Exclude from auto cleanup" checkbox set.
You find it in the release dialog, when creating a release and when editing one.

![exclude-from-auto-cleanup.png](images/exclude-from-auto-cleanup.png)

Use it for releases whose raw files or archives you want to keep on disk, no matter which retention periods are configured.

### Archive repackaging

"Repackaging strategy" controls how Bearcat changes newly generated archives for uploads and reuploads. The default is "Change archive file size by 1 MB".

Bearcat always writes a random `__nonce.txt` file into the release folder before packing.
The repackaging strategy decides how that nonce file should affect the generated archive files:

| Value | UI label | Behavior |
| --- | --- | --- |
| `NonceOnly` | Nonce only, no compression | Packs without compression and without solid mode. Only `__nonce.txt` changes. This has the lowest CPU cost, but the lowest chance that every archive part gets a new MD5 hash. |
| `SolidCompression` | Solid archive with compression | Packs with solid mode and compression. This is the safest option for making the nonce change affect all archive files, but it uses more CPU. |
| `IncrementArchiveFileSize` | Change archive file size by 1 MB | Packs without compression and without solid mode, but increases the archive part size by `1` MB compared to the latest archive for the same archive configuration. This is the default. |

`IncrementArchiveFileSize` stores the archive part size that was actually used on each archive.
When Bearcat creates the next archive for the same archive configuration, it reads the latest stored value and adds `1` MB.
For the first archive, Bearcat uses the "Archive file size (MB)" from the archive configuration.

RAR and 7Zip both support all three strategies.

### Folder automation

Bearcat creates a release from a watched folder once both conditions are met:

- Its total file count and size stay unchanged for "Folder stability (minutes)", default `5`.
- It reaches "Minimum folder size (MB)", default `1`.

This helps avoid creating releases and reading media metadata while files are still being copied.
Bearcat compares file count and size between scans because copied folders may retain their original modification dates.

Increase the stability period for slow or interrupted copies. Set the minimum size to `0` to allow empty folders.
These settings only affect automatic release creation. You can still create releases manually and extract metadata from the "Release info" panel.

### Initial upload cooldown

"Initial upload cooldown (minutes)" defaults to `5`. It gives you time to finish setting up a release before archiving and uploading begin.

Once an upload configuration exists and the release is older than the cooldown, the next "Upload state check" run creates the first upload record.
The wait is measured from release creation. Set it to `0` to allow the first upload on the next check.

Changes only affect upload configurations without an upload. Existing uploads are not canceled or delayed.

### Upload concurrency

"Maximum parallel uploads" defaults to `10`. It limits parallel file uploads across all hosters.
Each hoster's own limit also applies, so a hoster cannot exceed either limit.

View and, where allowed, override individual limits on the "Hoster registrations" page. See [Parallel uploads per hoster](/Bearcat/post-installation/#parallel-uploads-per-hoster).

Lower the global limit to reduce bandwidth and CPU usage. Raise it to transfer more files at once.
