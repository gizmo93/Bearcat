---
title: "Your First Upload"
description: "Add a hoster account, choose a folder, and upload your first release."
---

You need a running Bearcat installation, a hoster account, and a folder with files to upload.
A **release** is one set of files that you upload together.

Open Bearcat in your browser:

- **Desktop app or Windows service:** [http://127.0.0.1:17208](http://127.0.0.1:17208)
- **Docker:** [http://localhost:8080](http://localhost:8080), or `http://<server-address>:8080` on another computer

If you changed the web port during installation, use that port instead.

## 1. Add your hoster account

1. Open **Hoster registrations** and click **New hoster**.
2. Choose your hoster and enter its username and password or API key.
3. Save, then click **Try login** to check the connection.

Leave upload limits and reupload settings at their defaults.

<details>
<summary>Show hoster setup</summary>

![Register a hoster account](images/register-hoster.png)
![Test the hoster login](images/try-login.png)

</details>

## 2. Create a release group

A release group controls whether Bearcat automatically replaces offline uploads.

Open **Release groups**, click **New release group**, and name it `My uploads`.
Leave automatic reuploads disabled for now and save.
Bearcat still uploads your files and checks their links.

## 3. Choose your files

Put the files for your first upload in a subfolder of your working directory, for example
`releases/My.First.Upload/`.

1. Open **Releases** and click **New release**.
2. Select **Managed**. This means Bearcat creates the archives for you.
3. Choose the `My.First.Upload` folder. Leave the name empty to use the folder name.
4. Select the `My uploads` group and click **Create release**.
5. Click the release name to open its detail page.

In Docker, your `RELEASES_DIR` folder appears as `/mnt/data/releases` in the folder picker.
If you already have archives to upload, use an [unmanaged release](/Bearcat/release-types/#unmanaged-releases).

## 4. Choose how to create the archives

On the **Archive configurations** tab, click **Add**:

- Give the configuration a name, for example `Main archive`, and choose RAR or 7z.
- Choose an output folder within a working directory where Bearcat can write the archives. Use a separate folder from the source files.
- Set an archive file prefix, for example `My.First.Upload`, and a part size in MB that your hoster accepts. Bearcat adds the file extension.
- Leave the password empty unless you want password-protected archives, then save.

## 5. Choose where to upload

On the **Upload configurations** tab, click **Add**. Give it a name, select your hoster account
and the `Main archive` configuration, then save. Leave **Links distributed to** empty for now.

## 6. Wait for the upload and copy the links

Bearcat starts the work automatically. By default, it waits until the release is at least five
minutes old, then its background tasks create the archives and upload them.

Open **Uploads** to follow progress. Once the upload finishes, copy the download links from **Overview**.

![Release uploads](images/release-uploads-tab.png)

If the upload does not start or fails, check that **Try login** succeeds, both configurations are
saved, and the archive output folder is writable. Check the notification bell for error details.
See [Upload lifecycle](/Bearcat/upload-lifecycle/) for the individual stages.

## After your first upload

You can keep creating releases this way, or set up the features you need:

- [Save a template and watch folders](/Bearcat/release-templates-and-automations/) to reuse these settings.
- [Enable reuploads or add link crypters](/Bearcat/account-settings/) to manage your upload links.
- [Download from FTP or FTPS](/Bearcat/remote-downloads/) to fetch releases automatically.
- [Add metadata and cover images](/Bearcat/release-information-and-metadata/) for your releases.
- [Explore the release detail page](/Bearcat/release-detail-page/) to find its other settings and tabs.
