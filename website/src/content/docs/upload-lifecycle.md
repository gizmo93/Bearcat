---
title: "Upload Lifecycle"
description: "Understand how Bearcat creates, uploads, checks, and refreshes release archives."
---

This page explains what happens after you create a release and configure where it should be uploaded.

If you love complicated flowcharts, [this is the whole upload lifecycle](/Bearcat/images/upload-lifecycle.png).

## 1. Release and upload configuration

A release only describes the source folder and the setup around it.
Nothing is uploaded just because the release exists.

Bearcat starts the upload lifecycle once the release has at least one upload configuration.
An upload configuration connects these things:

- the release
- the hoster registration
- the archive configuration
- optional link crypter configurations

If a release has multiple upload configurations, each one gets its own upload lifecycle.
For example, one release can upload the same archive setup to Rapidgator and DDownload through two separate upload configurations.

## 2. Initial upload creation

The first upload is not created immediately when you save an upload configuration.
Bearcat waits for the "Initial upload cooldown" from "Configurations".
The default is `5` minutes.

The "Upload state check" background task creates missing initial uploads.
When it creates one, the upload starts in the `WaitingForArchive` state.

This cooldown gives you a short window to finish the release setup before Bearcat starts archiving and uploading.
If you set the cooldown to `0`, the initial upload can be created on the next upload state check.

## 3. Archive creation

The "Archive creation" background task looks for uploads in `WaitingForArchive` that do not have an archive yet.

For each matching upload, Bearcat checks whether there is already a finished archive for the same archive configuration.
If such an archive exists, Bearcat assigns it to the upload and moves the upload to `Pending`.

For reuploads, Bearcat also checks whether that archive has already been uploaded to the same hoster type before.
If it has, Bearcat must make sure the archive file hashes change before uploading again.
When the archiver supports changing hashes in place (only RAR currently), Bearcat appends a harmless trailing byte to the existing archive files before assigning the archive.
This changes the MD5 hash while keeping the archive extractable.

Not every archive format supports this safely.
If the configured archiver does not support changing hashes in place, Bearcat does not modify the existing files and creates a fresh archive instead.
If the reusable archive is currently used by another active upload, Bearcat waits and tries again on the next archive creation run.

If no reusable archive exists, Bearcat creates a new archive from the release folder.
The archive configuration decides:

- where archive files are written
- which archiver is used
- the archive file name prefix
- the archive password
- the archive part size

If archive creation succeeds, the archive becomes `Created` and the upload becomes `Pending`.
If archive creation fails, the archive becomes `CreationFailed` and the upload becomes `Failed`.

During archive creation, Bearcat writes a small `__nonce.txt` file with a random value into the release folder before packing.
This gives Bearcat a small, harmless file that can change between archive runs.
That is important for reuploads, because hosters may recognize an already uploaded archive file by its MD5 hash even if you upload it again later.

How strongly Bearcat changes the generated archive files is controlled by the "Archive repackaging" configuration.
The default strategy is "Change archive file size by 1 MB".
It creates the new archive without compression and without solid mode, but increases the archive part size by `1` MB compared to the latest archive for the same archive configuration.
That usually changes the resulting archive parts while avoiding compression CPU cost.

The other available strategies are:

- "Nonce only, no compression" changes only `__nonce.txt` and packs without compression or solid mode. This has the lowest CPU cost, but the lowest chance that every archive part gets a new MD5 hash.
- "Solid archive with compression" packs with solid mode and compression. This costs more CPU, but makes the `__nonce.txt` change affect the archive output more reliably.

If "Archive cleanup" is enabled later and the archive has already been deleted locally, Bearcat cannot reuse that deleted archive for future uploads.
In that case, a reupload will create a fresh archive if needed.

## 4. Upload to hoster

The "Archive upload" background task uploads files for `Pending` uploads.
While files are being uploaded, the upload state becomes `Uploading`.

When all archive files are uploaded successfully, the upload becomes:

- `Completed`
- `Online`
- marked with an upload timestamp

If some files fail to upload, the upload becomes:

- `Failed`
- `PartiallyOnline`

You can follow these runs in the release detail page under the "Uploads" tab.
The "Overview" tab shows the latest upload per upload configuration.

## 5. Link crypter containers

Link crypter containers are created after a hoster upload has completed successfully.

The "Link crypter container creation" background task processes uploads that are:

- `Completed`
- `Online`
- linked to uploaded hoster files
- connected to at least one active link crypter configuration

For the first successful upload of an upload configuration, Bearcat creates a new container for each configured link crypter.
That container contains the hoster links from the completed upload.

If the container creation fails, Bearcat stores the error on the container and creates a notification.

## 6. Online checks

The "Upload state check" background task regularly asks hosters whether uploaded files still exist.
Each file is checked again when it has never been checked before or when its last check is older than about 30 minutes.

The upload online state is updated from the file results:

- `Online` means all checked files are online.
- `PartiallyOnline` means at least one file is offline, but not all files are offline.
- `Offline` means all uploaded files are offline.

If a hoster check itself fails, Bearcat creates an error notification and keeps the previous online state.

## 7. Automatic reuploads

Automatic reuploads are controlled by release groups.
For a release to get automatic reuploads:

- its release group must have automatic reuploads enabled
- the latest relevant upload must be `Offline` or `PartiallyOnline`
- all uploaded files must have been checked at least once
- the release group's "Hours until reupload" threshold must be reached
- there must not already be an online or blocking replacement upload for the same upload configuration

Blocking replacement uploads are uploads that are already `Pending`, `Uploading`, `WaitingForArchive`, `Failed` or `CancellationRequested`.
Bearcat uses this rule to avoid creating many replacement uploads for the same upload configuration at the same time.

When Bearcat schedules a reupload, it creates a new upload record for the same upload configuration.
The new upload starts again at `WaitingForArchive`.
From there, the normal lifecycle continues:

```text
WaitingForArchive -> Pending -> Uploading -> Completed
```

If an existing archive can still be reused, the reupload can skip creating a new archive.
Otherwise Bearcat creates a new archive from the release folder.

## 8. Manual reuploads

You can create a manual reupload from the "Uploads" tab.
Bearcat allows this for uploads that are:

- `Offline`
- `PartiallyOnline`
- `Canceled`

The same blocking rules apply as with automatic reuploads.
If another online or pending replacement already exists for the upload configuration, Bearcat will not create another one.

Manual reuploads also create a new upload record and then use the normal archive and upload lifecycle.

## 9. What happens to link crypter containers on reupload?

On a reupload, Bearcat tries to keep the existing link crypter container link useful.

If an earlier upload for the same upload configuration already has a container for the same link crypter configuration, Bearcat tries to update that existing container with the new hoster links from the reupload.
This means the public container URL can stay the same while the links inside it are replaced.

If updating the existing container fails, Bearcat falls back to creating a new container.
In that case, the new upload can get a new container URL.

This behavior depends on the link crypter provider and its API.
Some providers support updating existing containers; if that does not work, Bearcat creates a fresh container instead.

## 10. Cleanup after successful uploads

Archive cleanup is optional.
If automatic archive cleanup is disabled, Bearcat keeps local archive folders after upload.

If automatic archive cleanup is enabled, the "Archive cleanup" background task deletes local archive folders after all linked uploads have completed.
This only deletes Bearcat's generated local archive folder.
It does not delete the release source folder and it does not delete files from the hoster.
