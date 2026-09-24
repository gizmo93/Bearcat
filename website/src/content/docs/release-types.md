---
title: "Managed Releases and Existing Archives"
description: "Understand managed and unmanaged releases in Bearcat."
---

Bearcat supports two release types: managed releases and unmanaged releases.
Both use the same upload, online check, notification, and reupload workflow.
They differ in who creates the archive files.

| Type | Source | Who creates archives? |
| --- | --- | --- |
| Managed | Raw release files | Bearcat |
| Unmanaged | Existing archives | You or another tool |

## Managed Releases

Managed releases are the default workflow.
The release folder contains the raw files, and Bearcat creates archive files based on the archive configuration.
You choose an archiver, archive folder, archive size, optional password, and the hosters where the release should be uploaded.

When an upload is needed, Bearcat creates or reuses a matching archive and uploads the archive files.
If local archive files go missing, Bearcat can restore them from a [mirror hoster](/Bearcat/mirror-downloads/)
or create a replacement archive from the raw release files.

## Unmanaged Releases

Unmanaged releases are for archives that already exist before Bearcat sees them.
Unlike managed releases, an unmanaged release has **no release folder** for raw files.
Instead, each archive configuration points directly at the folder that holds its archive files.
Bearcat creates an archive configuration and assumes the archiver based on the file endings.

If archive files are missing, the upload returns to `WaitingForArchive`. Bearcat restores the files
from an enabled [mirror hoster](/Bearcat/mirror-downloads/) if one still has them online. Otherwise,
you must provide the files: unmanaged releases cannot be repacked without raw files.

Mirror restores continue automatically. If you provide the files yourself, use the unmanaged archive
refresh action, or update the archive folder if you placed them elsewhere. Bearcat can then use them
for pending reuploads.

![unmanaged-releases-refresh-folder.png](images/unmanaged-releases-refresh-folder.png)

[Quality gates](/Bearcat/quality-gates/) only check the release infos (cover, description, NFO) of
unmanaged releases. There is no release folder to look at, so the other checks are skipped.

## Converting between types

You can convert a release in either direction from the actions menu on the release detail page.

- **Convert to unmanaged** is available for a managed release once every archive configuration has a created archive.
  Bearcat switches the release to unmanaged and forgets the raw release folder, but keeps the existing archives and uploads.
  It does not delete the raw files, so you can remove them yourself afterwards to save disk space.
- **Convert to managed** is available for an unmanaged release.
  You assign a release folder that holds the raw files, and Bearcat switches the release to managed while keeping the existing archive configurations and uploads.
  From then on Bearcat again can repack the release for reuploads.

For releases downloaded from FTP or FTPS, you can also turn off **Keep raw files** in the remote
automation. Bearcat then converts them to unmanaged and removes the downloaded folder after the
first uploads, once the [cleanup conditions](/Bearcat/remote-downloads/#delete-raw-files-after-uploading) are met.
