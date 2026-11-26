---
title: "Archive Storage Folders"
description: "Store archives on a NAS, another disk, or a network share after their retention period."
---

Storage folders let you move archives out of your working directories, for example to a NAS or
another disk. Bearcat checks that the folder is accessible and has enough space, but not whether
the expected drive is mounted there.

## Add a storage folder

Open **Configuration > Release settings > Archive storage folders** and click **New storage folder**.

| Field | Meaning |
| --- | --- |
| **Name** | Unique name for the storage folder. |
| **Path** | Absolute path of an existing folder. Each path can only be registered once. |
| **Minimum free space (GB)** | Space that must remain free after the move. Set `0` for no minimum. |
| **Parallel file copies** | Number of files per archive copied at once, both when moving into the folder and when copying into a local working copy. Raise it for network mounts (SMB, rclone); keep `1` for local hard disks. |
| **Priority** | Lower values take priority. |
| **Local working copy for reuploads** | Copy the files a reupload needs into a temporary local working copy. When off, read them directly from the storage folder. |

New storage folders are active. Use **Enable** or **Disable** in the row menu to change that.

## Path rules

Bearcat does not create the storage folder. It must already exist and must not overlap a working
directory: it cannot be inside one or contain one.

Use **Browse** to choose a folder on the machine running Bearcat.
In Docker, make it accessible through a bind mount and enter its path inside the container.

## Overview

The list shows free space, the required minimum, the number of stored archives, priority, and active status.
If free space cannot be read, it shows **Unavailable**.
Only current, complete archives count as stored. Bearcat excludes archives that were deleted,
replaced by a re-import, or have missing files.

## Edit or delete a storage folder

While a storage folder holds archives, you cannot change its path or delete it.
You can still change the other settings.

Deleting a storage folder only removes it from Bearcat. The folder and its contents stay on disk.
Archives that no longer count as stored lose their reference to the deleted storage folder.

## Moving archives

Under [Auto cleanup](/advanced-configuration/#local-archives-after-retention), set
**Local archives after retention** to **Move to storage folder**.
Bearcat then moves archives once their retention period has passed.

For each archive, Bearcat chooses an active, accessible folder with enough space for all archive
files plus the required free space. The lowest **Priority** value takes precedence.
At the same priority, the folder with the most free space wins.
An archive always stays together in one subfolder named `<release name>.<archive config name>.<archive ID>`,
so you can tell archives apart without Bearcat. Characters that are not allowed in folder names become `_`.

Bearcat copies the files and checks their sizes before using the new location and deleting the
original archive files. It removes the original archive folder only if it is empty.
Archive files that are missing locally do not block the move. They stay missing until a reupload
restores them.

Under **Home**, you can follow progress and see the destination. **Cancel move** removes the
files copied so far and keeps the archive at its original location.

If no folder fits or copying fails, the archive stays at its original location.
Bearcat notifies you and retries on the next run.
No further notifications are sent for the same error until you resolve the current one.

Archives in storage folders are never deleted automatically.
**Delete local archives** on the release detail page skips them as well.

## Reuploads

When a reupload needs an archive from a storage folder, **Local working copy for reuploads** decides
how Bearcat uses it.

**Off:** the upload reads the archive files directly from the storage folder. Hash changes for the
reupload are written there too, so the storage folder must be writable.

**On:** Bearcat copies only the archive files that need uploading into a temporary local working copy.
The working copy is inside the **Archive files base path** set in the archive configuration.
Its subfolder has the same name as the archive folder. If that subfolder already contains other files, Bearcat uses
`<folder name>.<archive ID>` instead. Files that are still in a working copy from an earlier reupload
are reused.

- The upload waits until the copy has finished.
- Hash changes and the upload use the local files.
- The storage folder is never written to by a reupload. The archive stays assigned to its storage folder.
- After all uploads for that archive configuration finish, the
  [Auto cleanup](/advanced-configuration/#available-background-tasks) task points the archive
  back to the storage folder and deletes the working copy. This runs regardless of the cleanup rules.
  Failed uploads do not delay it.

Under **Home**, **Copying into local working copies** shows the progress.
**Cancel copying** removes the files copied so far and the upload reads the archive from the storage folder.

If copying fails, Bearcat removes any copied files and falls back to reading the archive from the storage
folder. This also applies if the **Archive files base path** does not exist: Bearcat creates only the
subfolder inside it, never the base path itself. You get one notification per archive until you resolve it.

If archive files are missing in the storage folder, Bearcat downloads only the missing files from a
[mirror](/mirror-downloads/) and puts them back into the archive's subfolder there.
Without a mirror, it repacks managed releases. If the storage folder is not available, the restore fails
with a notification instead of downloading. Bearcat never creates the storage folder.

## Network mounts

On network mounts with high latency, such as a remote SMB share, the mount can limit transfer speed.
The system commands below apply to Linux, including Synology NAS devices.

- **Parallel file copies:** copying several files at once can be faster over a high-latency connection.
  Try setting this to `6`.
- **Readahead:** if the kernel reads too little ahead on SMB (CIFS) mounts, copying into local working
  copies can be slow. Try 16 MiB:

  ```bash
  for f in /sys/class/bdi/cifs-*/read_ahead_kb; do echo 16384 > "$f"; done
  ```

  The value resets on every remount. If your kernel supports the `rasize` mount option, you can use
  `rasize=16777216` instead.
- **TCP buffers:** if transfers use only a small part of the available bandwidth, higher buffer limits
  may help:

  ```bash
  sysctl -w net.core.wmem_max=67108864 net.core.rmem_max=67108864
  sysctl -w net.ipv4.tcp_wmem="4096 65536 67108864" net.ipv4.tcp_rmem="4096 131072 67108864"
  ```

Run the readahead and TCP buffer commands as root after every boot, once the shares are mounted.
On Synology, you can create a boot-up task in Task Scheduler. Since DSM can remount shares, you can
also schedule an hourly task to reapply the readahead value.

Choose a storage location with low latency to the machine running Bearcat.

## Where stored archives are shown

The **Archives** tab of the release detail page and the **Archives** list show the storage folder name
next to archives stored there. Unmanaged releases also show it next to the archive folder in the header.
While an archive has a local working copy, the name is followed by **local working copy**.

When you change the archive folder of an unmanaged release, Bearcat assigns the archive to whichever
storage folder contains the new path, or to none if it lies outside all storage folders.
