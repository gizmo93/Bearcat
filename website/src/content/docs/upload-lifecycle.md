---
title: "Automatic Uploads, Link Checks, and Reuploads"
description: "Understand how Bearcat creates, uploads, checks, and refreshes release archives."
---

After you add an upload configuration, Bearcat prepares the archive, uploads it, and checks its links.
Release groups can also enable automatic reuploads when files go offline.

<details>
<summary>Show the full upload lifecycle diagram</summary>

```mermaid
flowchart TD
    %% --- Setup ---
    Release[Release] --> UploadConfig[Upload configuration]
    UploadConfig --> Cooldown[Initial upload cooldown]
    Cooldown --> WFA[Upload record: WaitingForArchive]

    %% --- Archive decision ---
    WFA --> QReuse{Reusable archive exists?}
    QReuse -- Yes --> QHosterType{Archive already uploaded to this hoster type?}
    QReuse -- No --> QManaged{Managed release?}

    QHosterType -- No --> Assign[Assign existing archive]
    QHosterType -- Yes --> QActive{Reusable archive active in another upload?}

    QActive -- Yes --> WaitTick[Wait for next archive creation tick]
    WaitTick --> WFA
    QActive -- No --> QInPlace{Archiver supports in-place hash change?}

    QInPlace -- "Yes (RAR)" --> Append[Append 0-bytes until each file's hash is new]
    Append --> Assign
    QInPlace -- "No (e.g. 7-Zip)" --> QManaged

    QManaged -- Yes --> Create[Create archive with __nonce.txt]
    QManaged -- "No (bring your own archive)" --> WaitTick

    Create --> QCreated{Archive created?}
    QCreated -- No --> Failed[Upload: Failed]
    QCreated -- Yes --> Pending[Upload: Pending]
    Assign --> Carry[Carry over still-online files from previous upload]
    Carry --> Pending

    %% --- Upload to hoster ---
    Pending --> UploadHoster[Upload to hoster]
    UploadHoster --> QAllFiles{All files uploaded?}
    QAllFiles -- Yes --> Completed[Upload: Completed / Online]
    QAllFiles -- No --> FailedPartial[Upload: Failed / PartiallyOnline]

    %% --- Link crypter containers ---
    Completed --> QLinkCrypter{Link crypter configured?}
    QLinkCrypter -- No --> OnlineChecks[Online checks]
    QLinkCrypter -- Yes --> QPrevContainer{Previous container exists?}
    QPrevContainer -- Yes --> UpdateContainer[Update existing container]
    QPrevContainer -- No --> NewContainer[Create new container]
    UpdateContainer --> OnlineChecks
    NewContainer --> OnlineChecks

    %% --- Online checks ---
    OnlineChecks --> QCaptcha{Captcha required?}
    QCaptcha -- Yes --> CaptchaWait[Mark upload + notify: resolve captcha]
    CaptchaWait --> OnlineChecks
    QCaptcha -- No --> QOnline{Files still online?}
    QOnline -- Yes --> OnlineChecks
    QOnline -- No --> Offline[Upload: Offline / PartiallyOnline]

    %% --- Cancel (manual) ---
    UploadHoster -. cancel requested .-> Canceled[Upload: Canceled]

    %% --- Reupload: automatic from Offline/PartiallyOnline, manual also from Failed/Canceled ---
    Offline --> QReupload{Reupload allowed?}
    FailedPartial -. manual reupload .-> QReupload
    Failed -. manual reupload .-> QReupload
    Canceled -. manual reupload .-> QReupload

    QReupload -- No --> WaitReupload[Wait for manual action or release group threshold]
    WaitReupload --> QReupload
    QReupload -- Yes --> NewUpload[New upload record]
    NewUpload --> WFA
```

</details>

## 1. Release and upload configuration

Creating a release does **not** upload anything yet. Add an **upload configuration** to connect:

- the release
- the hoster registration
- the archive configuration
- optional link crypter configurations

Each upload configuration runs independently. To upload a release to two hosters, create one
configuration for each. You can also use separate configurations for different archive sizes on the same hoster.

## 2. The first upload

Bearcat waits for the **Initial upload cooldown** before creating the first upload. Set it under
**Configurations**; the default is `5` minutes. This gives you time to finish setup and copy the release files.

After the cooldown, the **Upload state check** background task creates the upload in the
`WaitingForArchive` state. Set the cooldown to `0` to create it on the next check.

## 3. Creating the archive

The **Archive creation** background task prepares an archive for each upload in `WaitingForArchive`:

1. Reuse a finished archive for the same archive configuration, if possible.
2. Restore missing local files from a configured [mirror hoster](/Bearcat/mirror-downloads/).
3. If neither is possible, create a new archive from the release folder.

Only [managed releases](/Bearcat/release-types/) can be repacked. Unmanaged releases need existing
archive files or a mirror from which to restore them.

The archive configuration sets the archiver, output folder, filename prefix, password, and part size.
After packing succeeds, the archive becomes `Created` and the upload moves to `Pending`. If packing
fails, their states become `CreationFailed` and `Failed`.

For reuploads, Bearcat may need to change the archive hashes or repack the files. See
[Archive reuse and repackaging](#archive-reuse-and-repackaging) for the details.

## 4. Uploading to the hoster

The **Archive upload** background task starts `Pending` uploads and changes their state to `Uploading`.

| Result | Upload state | Online state |
| --- | --- | --- |
| All files uploaded | `Completed` | `Online` |
| Some files failed | `Failed` | `PartiallyOnline` |

Bearcat records a timestamp for successful uploads. Follow progress on the release detail page
under **Uploads**. **Overview** shows the latest upload for each upload configuration.

## 5. Link crypter containers

Once a hoster upload finishes successfully, Bearcat can wrap the links into link crypter containers.

The **"Link crypter container creation"** background task processes uploads that are:

- `Completed`
- `Online`
- linked to uploaded hoster files
- connected to at least one active link crypter configuration

For the first successful upload of an upload configuration, Bearcat creates a fresh container for each configured link crypter, filled with that upload's hoster links. If a container can't be created, Bearcat records the error on the container and raises a notification so you know.

This runs per release. If you manage related releases together (a TV show season, for example), a release collection can bundle their links into one shared container instead of one per release. See [Release Collections](/Bearcat/release-collections/) for the details.

## 6. Online checks

The **"Upload state check"** background task regularly asks hosters whether the uploaded files are still there. A file gets re-checked when it has never been checked, or when its last check is older than 30 minutes.

Bearcat will then classify the upload using the following states:

- **`Online`**: every checked file is online.
- **`PartiallyOnline`**: at least one file is offline, but not all of them.
- **`Offline`**: all uploaded files are offline.

If the check itself fails (the hoster errors out), Bearcat raises an error notification and keeps the previous online state. And if a hoster asks for a captcha verification first, Bearcat marks the upload accordingly and sends you a notification that asks you to resolve the captcha.

## 7. Automatic reuploads

Bearcat creates an automatic reupload when all of these conditions are met:

- its release group has automatic reuploads enabled
- the latest relevant upload is `Offline` or `PartiallyOnline` (subject to the hoster's reupload trigger, see below)
- every uploaded file has been checked at least once
- the waiting time ("Hours until reupload") has passed since the upload went offline
- there isn't already an online or *blocking* replacement upload for the same upload configuration

A replacement upload blocks another reupload if it is `Online` or has the state `Pending`,
`Uploading`, `WaitingForArchive`, `Failed`, or `CancellationRequested`. This prevents duplicate
replacements for the same upload configuration.

When a reupload is due, Bearcat creates a new upload record for the same upload configuration:

```text
WaitingForArchive -> Pending -> Uploading -> Completed
```

If it reuses the same archive as a previous upload, Bearcat keeps that upload's still-online links
and uploads only the offline files. A newly packed archive must be uploaded in full because its
parts cannot be combined with the old ones.

### Per-hoster overrides

A hoster registration can override the release group's waiting time and trigger. See
[Reupload overrides per hoster](/Bearcat/post-installation/#reupload-overrides-per-hoster) for setup.

- **Hours until reupload:** replaces the release group's waiting time for this hoster.
- **Reupload trigger:** controls when that waiting time starts:
  - **Partially or fully offline** (default): starts when the first file goes offline.
  - **Only when fully offline:** starts when the last file goes offline. No reupload is scheduled
    while any file remains online. If a file comes back online, the timer resets.
- **Always reupload all files:** uploads every archive part, including those that are still online.

For hosters that remove files one at a time, **Only when fully offline** avoids repeated reuploads
while parts are still online. If files expire after a period without downloads, **Always reupload
all files** gives them a new upload date together.

## 8. Manual reuploads

You can also trigger a reupload yourself from the **"Uploads"** tab. Bearcat allows this for uploads that are:

- `Offline`
- `PartiallyOnline`
- `Canceled`
- `Failed`

The same blocking rules as for automatic reuploads apply: if another online or in-progress replacement already exists for the upload configuration, Bearcat won't create a second one.

A manual reupload creates a new upload record and then follows the normal archive-and-upload lifecycle from there.

## 9. What happens to link crypter containers on a reupload?

Bearcat tries to update the existing container for the same upload configuration and link crypter
configuration.
If the update succeeds, the URL stays the same, so existing forum posts need no changes.

If the provider cannot update containers or the update fails, Bearcat creates a new container.
Its URL may differ from the old one.

## 10. Cleanup after a successful upload

Cleanup is optional and both of its rules are disabled by default. With auto cleanup off, Bearcat keeps your release folder and your local archive files.

With it on, the **"Auto cleanup"** background task works on retention periods:

- After the release folder period, a managed release is converted to unmanaged and you get a notification with the release folder path. Bearcat never deletes that folder itself.
- After the archive period, the local archive files are deleted, but only when the archives can be restored from a mirror hoster or repacked from a release folder.

The archive period counts from the last upload of the archive configuration, so every reupload restarts it. Nothing is ever deleted from the hoster.

Once the local archive is gone, a later reupload gets its files back from a [mirror hoster](/Bearcat/mirror-downloads/), and falls back to repacking for managed releases. See [Auto cleanup](/Bearcat/advanced-configuration/#auto-cleanup) for the settings and the exact preconditions.

## Archive reuse and repackaging

Hosters often recognise files by their MD5 hash. If an archive was previously uploaded to the same
type of hoster, Bearcat needs new hashes before uploading it again.

- **RAR:** Bearcat appends zero bytes until each file has a hash not yet used for this archive
  configuration. The archive still extracts normally. Bearcat keeps the hash history even after
  local files are deleted.
- **7-Zip:** split volumes cannot be changed this way without corrupting the archive, so Bearcat
  creates a new archive instead.

Before changing a reused archive, Bearcat checks whether another active upload is using it.
If so, it waits until a later run.

### The nonce file and repackaging

Before packing, Bearcat writes a new random value to `__nonce.txt` in the release folder.
The changed content helps produce different archive hashes.

For RAR, the nonce and appended zero bytes ensure each part gets a new hash. Parts remain close to
the configured size, with only a few extra bytes. This matters when a hoster limits file sizes.

For 7-Zip, **Archive repackaging** controls how Bearcat produces different files:

| Strategy | Packing settings | Trade-off |
| --- | --- | --- |
| **Change archive file size by 1 MB** (default) | No compression or solid mode; part size grows by `1` MB from the latest archive | Changes the parts without compression work |
| **Nonce only, no compression** | Changes only `__nonce.txt`; no compression or solid mode | Low CPU use, but some parts may keep their old hash |
| **Solid archive with compression** | Solid mode and compression | Higher CPU use; the nonce change affects the archive more reliably |

The compression and solid-mode settings also apply to RAR, but the `1` MB size increase does not.
RAR archives created before hash tracking was introduced are repacked once using the selected
strategy. Later reuploads can use the appended-byte method.
