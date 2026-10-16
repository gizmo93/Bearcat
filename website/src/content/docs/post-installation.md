---
title: "Your First Upload"
description: "Connect a hoster, create a release template, and add a source with the setup wizard."
---

Have your hoster account and files ready. The files you upload together form a **release**.

Open Bearcat in your browser:

- **Desktop app or Windows service:** [http://127.0.0.1:17208](http://127.0.0.1:17208)
- **Docker:** [http://localhost:8080](http://localhost:8080), or `http://<server-address>:8080` on another computer

If you changed the web port during installation, use that port instead.

## Open the setup

On a fresh installation, **Setup** opens automatically the first time you open the start page.
It opens automatically only once. Later, open **Setup** under **System** in the navigation, or find it
with **Ctrl+K** or **Cmd+K**.

You can skip every step and change everything later.

### Welcome

Click **Start setup**. **Skip setup** returns to the start page. To set up Bearcat without the wizard,
see [Set up manually](#set-up-manually).

### Working directories

Bearcat only sees folders inside its working directories. The step lists each one as **OK**,
**Missing**, or **Not writable**. You need at least one existing, writable working directory to continue.

- **Docker:** the host folder `RELEASES_DIR` from your `.env` file appears as `/mnt/data/releases`.
- **Desktop app or Windows service:** set working directories in the desktop app settings or in
  `appsettings.user.json`, then restart Bearcat and click **Check again**.

The working directories cannot be changed in the wizard.

### Hoster

1. Click **Add hoster**, choose your hoster, and enter its username and password or API key.
   Leave upload limits and reupload settings at their defaults.
2. Select the registrations Bearcat should upload to.
3. Click **Try login** for each selected registration.

**Next** is available once at least one selected registration has a successful login.

<details>
<summary>Show hoster setup</summary>

![Register a hoster account](images/register-hoster.png)

</details>

### Release group and template

The release group decides whether Bearcat reuploads offline links. The release template holds the
archive settings and target hosters that automations use for every new release.

If templates already exist, choose **Use existing template** or **Create new template**. For a new template:

| Field | Default |
| --- | --- |
| **Release group** | `Default`, or the first group if `Default` does not exist |
| **Template name** | `Default` |
| **Archiver** | RAR |
| **Part size (MB)** | The smallest file size limit of the selected hosters. `0` means no splitting; 7z needs a value greater than `0`. |
| **Archive password** | Empty, no password |
| **Archive output folder** | `bearcat-archives` in the first writable working directory |

The wizard does not create a release group. Below **Release group** it shows whether the selected group
reuploads offline links automatically. Change this on **Release groups**. Without any release group, the
step links to **Release groups** instead of showing the form.

Keep the archive output folder separate from your source folders. If it does not exist yet, click
**Create folder**. Then click **Create and continue**.

Bearcat creates a managed template with one archive configuration and one upload configuration per
selected hoster. Without a selected hoster, the template has no uploads. Add hosters later on
**Release templates**. Archives get a random name. To use the release name instead, edit the archive
configuration on **Release templates**.

### Source

A source is where new releases come from. Choose **Local folder** or **FTP/FTPS server**.

**Local folder:**

1. Set **Release base path**. Each direct subfolder becomes a release.
2. Optionally set a **Folder name pattern**, such as `*1080p*`.
3. For RAR or 7z archives in the folders, turn on **Extract archives before release creation**.
   See [Extract archives before release creation](/Bearcat/release-templates-and-automations/#extract-archives-before-release-creation).

The **Folder preview** lists the matching folders and how many are already in use. Archive output
folders count as used and never become releases.

If matching folders without a release exist, choose what happens with them under **Existing folders**:

| Option | Result |
| --- | --- |
| **Upload one as a test** (default) | Bearcat creates a release for the selected **Test folder** right away and starts its uploads without the initial upload cooldown. The other existing folders are ignored. Not available with archive extraction. |
| **Only new folders** | Existing folders are ignored. Only folders added later become releases. |
| **Import all** | Every existing folder becomes a release and is uploaded. |

Pick a small test folder to check archives and uploads quickly. Click **Create source**.

**FTP/FTPS server:**

1. Without an active remote source, click **Add remote source** and enter your server details.
2. Click **Create remote automation**. In the dialog, choose the remote source, the remote folder to
   watch, and the local download folder.

Existing folders on the server are ignored. See [FTP / FTPS Downloads](/Bearcat/remote-downloads/)
for the settings.

After the source is created, Bearcat confirms it and shows the upload result of the test release.
**Open release** opens the test release. **Go to activity** opens the start page.

## What happens next

- **Test release:** packing starts shortly after you click **Create source**.
- **New local folders:** Bearcat scans about every two minutes. It creates the release once the folder
  has stayed unchanged for the [folder stability](/Bearcat/advanced-configuration/#folder-automation)
  period (default: 5 minutes).
- **New folders on the server:** Bearcat downloads a folder once it has stayed unchanged on the server
  for the [stability period](/Bearcat/advanced-configuration/#remote-downloads) (default: 5 minutes),
  then creates the release.
- **Uploads** start after the [initial upload cooldown](/Bearcat/advanced-configuration/#initial-upload-cooldown)
  (default: 5 minutes). To skip the wait for one release, click **Start uploads now** on its detail page.

Running archive creations and uploads appear on the start page. On the release detail page, open
**Uploads** and switch to **History** to follow progress. Once the upload finishes, copy the download
links from **Overview**.

![Release uploads](images/release-uploads-tab.png)

If the upload fails or does not start, check the notification bell for errors. Make sure **Try login**
succeeds and the archive output folder is writable. See [Upload lifecycle](/Bearcat/upload-lifecycle/)
for the stages.

### Start page

While the hoster, release template, or source is missing, a banner on the start page shows what is
missing. **Continue setup** opens the wizard again.

After the required setup, the start page shows **Optional next steps**: metadata and NFOs, an image
hoster for cover images, link crypters, Telegram notifications, forum posting, and proxy servers.
Each item links to its settings page and is marked as done once set up. Click **Hide** to hide the list.

## Set up manually

1. **Hoster:** open **Hoster registrations**, click **New hoster**, enter your credentials, save, and
   click **Try login**.
2. **Release group:** open **Release groups**, click **New release group**, and save. Keep automatic
   reuploads disabled for a first test.
3. **Release:** put your files in a subfolder of a working directory, for example `releases/My.First.Upload/`.
   Open **Releases**, click **New release**, select **Managed**, choose the folder and the group, and
   click **Create release**. In Docker, `RELEASES_DIR` appears as `/mnt/data/releases`. For archives you
   already have, use an [unmanaged release](/Bearcat/release-types/#unmanaged-releases).
4. **Archives:** on the release's **Archives** tab, click **Add**. Choose RAR or 7z, a writable output
   folder in a working directory separate from your source files, a file prefix, a part size within
   your hoster's file size limit, and an optional password.
5. **Uploads:** on **Uploads** > **Configuration**, click **Add**, select your hoster account and the
   archive configuration, and save. Leave **Links distributed to** empty for now.

The upload starts after the initial upload cooldown, or right away with **Start uploads now**.

<details>
<summary>Show the login test</summary>

![Test the hoster login](images/try-login.png)

</details>

To create releases from new folders automatically, save the settings as a template and add a
[folder automation](/Bearcat/release-templates-and-automations/).

## After your first upload

For future uploads, you can:

- [Save a template and watch folders](/Bearcat/release-templates-and-automations/) to reuse these settings.
- [Enable reuploads or add link crypters](/Bearcat/account-settings/) to manage your upload links.
- [Download from FTP or FTPS](/Bearcat/remote-downloads/) to fetch releases automatically.
- [Add metadata and cover images](/Bearcat/release-information-and-metadata/) for your releases.
- [Explore the release detail page](/Bearcat/release-detail-page/) for more settings.
