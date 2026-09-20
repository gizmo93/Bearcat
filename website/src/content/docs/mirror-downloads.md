---
title: "Use Hosters as Long-Term Storage"
description: "Let Bearcat download archive files back from a hoster when a reupload needs them, instead of keeping every archive on local disk."
---

Enable a hoster as a mirror so Bearcat can restore archive files when a reupload needs them.
You can then delete local archives to free disk space, as long as a mirror still has the required files online.

This works for both [managed and unmanaged releases](/Bearcat/release-types/).

![running-mirror-download.png](images/running-mirror-download.png)

## When Bearcat downloads from a mirror

A mirror download happens when all of the following are true:

- An upload is waiting for its archive, which is the normal state for a new reupload.
- The newest archive of that archive configuration is deleted or has missing files.
- A mirror hoster still has every needed file online.

Bearcat looks for archive files in this order:

1. Use the local archive files if they are still on disk.
2. Download them from a mirror hoster.
3. For managed releases only, repack them from the release folder.

An unmanaged release without a mirror has nothing to fall back on, so Bearcat keeps the upload
waiting and asks you to provide the archive files. See
[Unmanaged Releases](/Bearcat/release-types/#unmanaged-releases) for that path.

Bearcat only downloads the files it actually needs. If a previous upload to the same hoster still
has some files online, those are carried over and only the missing ones are downloaded.

## Enable a hoster as a mirror

Open the hoster registration and turn on **Use for mirror downloads**.

![enable-hoster-mirror-download.png](images/enable-hoster-mirror-download.png)

### Hosters that support mirror downloads

Not every hoster allows file downloads using its API, so the **"Use for mirror downloads"**
switch only shows up for the ones that do:

| Hoster | Premium account needed |
| --- | --- |
| Rapidgator | Yes |
| 1fichier | Yes |
| Alfafile | Yes |
| HxFile.co | Yes |
| Keep2Share | Yes |
| Fast2Share.com | Yes |
| DDownload | No |
| datavaults.co | No |
| file-upload.org | No |
| Uploady.io | No |

Bearcat shows a hint beside the switch when a premium account is needed. Free accounts can still
upload to these hosters.

<details>
<summary>Why some hosters need a premium account</summary>

- **Alfafile:** free accounts allow one download every 120 minutes and only 500 MB of total traffic.
- **Fast2Share:** free accounts have a waiting period for each file. Bearcat does not support this,
  so mirror downloads with a free account fail.
- **Keep2Share:** free downloads are limited to roughly 50 KB/s and one connection at a time.

</details>

## Cancel a running download

Running mirror downloads show up on the start page under "Running transfers", with progress, speed
and a per file breakdown. Each row has a cancel button.

Canceling discards the partial download and cancels the uploads that were waiting for that archive,
so they do not start over on the next run. The archive goes back to the state it had before, and
your files on the hoster are not touched. Create a manual reupload when you want to try again.

## Verification and failures

Bearcat stores the MD5 hash of every file it uploads. After a mirror download it hashes the
downloaded file again and compares it to the stored value. If a hash does not match, or a download
fails for another reason, Bearcat discards the restore, keeps the archive in its old state, and
creates an "Archive restore failed" notification.

A failed restore also fails the waiting uploads. Create a manual reupload once you fixed the cause.

## Parallel downloads

The "Mirror downloads" section on the "Configurations" page has one setting:

- **"Max parallel downloads"** defaults to `2`. It limits how many archive files Bearcat downloads
  at the same time while restoring an archive.

Raise it if your line is fast and your hoster account allows several connections. Lower it to `1` if
the hoster throttles or rejects parallel downloads.

## Delete local archives yourself

The release detail page has a **"Delete local archives"** action in the actions menu. It deletes the
local archive files of the release and marks the archives as deleted, which is the manual way to
free disk space.

![delete-local-archives.png](images/delete-local-archives.png)

Bearcat only offers this when the release stays recoverable:

- No upload of the release is running.
- Every archive configuration has a fully online upload on a hoster that is enabled for mirror
  downloads, **or** the release is managed and still has its release folder for repacking.

The action deletes the archive files one by one and removes the archive folder only if it is empty
afterwards. That matters for unmanaged releases, where the archive folder is your folder and may
hold other files.

## Automatic cleanup

Auto cleanup can delete local archives on a retention period and
leave the restore to the mirrors. See
[Auto cleanup](/Bearcat/advanced-configuration/#auto-cleanup) for the two rules and their settings.
