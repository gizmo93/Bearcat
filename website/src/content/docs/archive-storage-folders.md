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
| **Local working copy for reuploads** | Copy the files a reupload needs into a temporary local working copy. When off, read them directly from the storage folder. |

New storage folders are active. Use **Enable** or **Disable** in the row menu to change that.

## Path rules

Bearcat does not create the storage folder. It must already exist and must not overlap a working
directory: it cannot be inside one or contain one.

Use **Browse** to choose a folder on the machine running Bearcat.
In Docker, make it accessible through a bind mount and enter its path inside the container.

## Overview

The list shows free space and its required minimum, the number of stored archives, priority, and active status.
If free space cannot be read, it shows **Unavailable**.
Only current archives count as stored. Archives that were deleted, replaced by a re-import, or have
missing files do not.

## Edit or delete a storage folder

While a storage folder holds archives, you cannot change its path or delete it.
You can still change the other settings.

Deleting a storage folder only removes it from Bearcat. The folder and its contents stay on disk.
Archives that no longer count as stored lose their reference to the deleted storage folder.

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

Archives in storage folders are never deleted automatically.
**Delete local archives** on the release detail page skips them as well.

## Reuploads

When a reupload needs an archive from a storage folder, **Local working copy for reuploads** decides
how Bearcat uses it.

**Off:** the upload reads the archive files directly from the storage folder. Hash changes for the
reupload are written there too, so the storage folder must be writable.

**On:** Bearcat copies only the archive files that need uploading into a temporary local working copy.
The working copy is a subfolder with the archive's folder name inside the **Archive files base path**
of the archive configuration. If that subfolder already contains other files, Bearcat uses
`<folder name>.<archive ID>` instead. Files that are still in a working copy from an earlier reupload
are reused.

- The upload waits until the copy has finished.
- Hash changes and the upload use the local files.
- The storage folder is never written to by a reupload. The archive stays assigned to its storage folder.
- After the uploads of the archive configuration have finished, the
  [Auto cleanup](/Bearcat/advanced-configuration/#available-background-tasks) task points the archive
  back to the storage folder and deletes the working copy. This runs regardless of the cleanup rules.
  Failed uploads do not delay it.

Under **Activity**, **Copying into local working copies** shows the progress.
**Cancel copying** removes the files copied so far and the upload reads the archive from the storage folder.

If copying fails, Bearcat removes the copied files and the upload reads the archive from the storage
folder. This also applies when the **Archive files base path** does not exist: Bearcat never creates
it, only the subfolder inside it. Bearcat notifies you once per archive until you resolve the notification.

If the storage folder is unreachable or archive files are missing there, Bearcat handles the archive like
any archive with missing files: it restores the files from a [mirror](/Bearcat/mirror-downloads/) or
repacks managed releases. Bearcat never creates the storage folder.

## Where stored archives are shown

The **Archives** tab of the release detail page and the **Archives** list show the storage folder name
next to archives stored there. Unmanaged releases also show it next to the archive folder in the header.
While an archive has a local working copy, the name is followed by **local working copy**.

When you change the archive folder of an unmanaged release, the archive belongs to the storage folder
that contains the new folder, or to none.
