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
| **Priority** | Lower values take priority. |
| **Copy archives back before reupload** | Copy archives to local disk before reuploading. When off, read them directly from the storage folder. |

New storage folders are active. Use **Enable** or **Disable** in the row menu to change that.

## Path rules

Bearcat does not create the storage folder. It must already exist and must not overlap a working
directory: it cannot be inside one or contain one.

Use **Browse** to choose a folder on the machine running Bearcat.
In Docker, make it accessible through a bind mount and enter its path inside the container.

## Overview

The list shows free space and its required minimum, the number of stored archives, priority, and active status.
If free space cannot be read, it shows **Unavailable**.

## Edit or delete a storage folder

While a storage folder holds archives, you cannot change its path or delete it.
You can still change the other settings.

Deleting a storage folder only removes it from Bearcat. The folder and its contents stay on disk.

## Moving archives

Under [Auto cleanup](/Bearcat/advanced-configuration/#local-archives-after-retention), set
**Local archives after retention** to **Move to storage folder**.
Bearcat then moves archives once their retention period has passed.

For each archive, Bearcat chooses an active, accessible folder with enough space for all archive
files plus the required free space. The lowest priority takes precedence.
At the same priority, the folder with the most free space wins.
An archive always stays together in one subfolder.

Bearcat copies the files and checks their sizes before using the new location and deleting the
original archive files. It removes the original archive folder only if it is empty.

Under **Activity**, you can follow progress and see the destination. **Cancel move** removes the
files copied so far and keeps the archive at its original location.

If no folder fits or copying fails, the archive stays at its original location.
Bearcat notifies you and retries on the next run.
Further notifications for the same error wait until you resolve the current one.

Archives in storage folders are never deleted automatically. Reuploads read them directly from there,
unless **Copy archives back before reupload** is enabled.
