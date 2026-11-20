---
title: "Import Existing Link Crypter Folders"
description: "Create releases from existing FileCrypt, Hide.cx or ToLink folders and keep their links."
---

Bearcat reads the folders in your link crypter account and creates a release for each release name.
It imports mirrors with valid archive links as completed uploads, then uploads the files to the remaining hosters in the release template.

## Supported link crypters

- **FileCrypt**: the API key needs the `LIST` permission to list your folders.
- **Hide.cx**
- **ToLink**: a folder lists all links together. Bearcat groups them into one mirror per hoster domain.

## Steps

1. Open **Registrations > Crypters** > **Link crypter imports** and click **New import**.
2. Choose the crypter registration and the **Default release template** for new releases. The template sets the hosters, archive settings and link crypter settings. You can also choose a **Series template** and a **Default language**. See [Template and language](#template-and-language) for details.
3. If a release has no local folder, Bearcat can restore its archives from the imported mirrors. To choose where to save them, set the **archive base folder for releases without local files**.

   ![New import dialog](images/link-crypter-import-new-import-dialog.png)

4. Click **Read crypter folders**. Bearcat reads the folders and links in the background and skips folders whose URLs it already knows.

   ![Import reading the crypter folders](images/link-crypter-import-reading-folders.png)

5. Select one hoster account per hoster and click **Check files**. Bearcat checks the files with this account only.

   ![Hoster accounts for the file check](images/link-crypter-import-hoster-accounts-for-file-check.png)

6. Review the releases, select the ones to import and click **Start import**.

## Review

![Review of the releases to import](images/link-crypter-import-review.png)

Bearcat uses the folder name as the release name and groups folders with the same release name into one entry. Use **Rename release** to correct a name. If you enter a name already in the list, Bearcat groups the folders under that name and uses that entry's template and language.

### Find and select releases

Enter any part of a release or folder name. The search ignores case. For example, `Show.S01*-GRP` finds names that start with `Show.S01` and end with `-GRP`. Use the status filter to narrow the results.

After the file check, Bearcat selects all importable folders. Use the checkbox next to a release to select or clear its folders together. **Select all matches** applies to every page of the filtered results.

- **Change release template** and **Change primary language** apply to the selected releases that match the current search and status filter. To edit one release, search for its name and select just that match. You can also edit releases marked **Not importable**.
- If you chose a series template, you can switch between the default and series templates. Bearcat then updates the preview. This can take a while for large imports.
- **Start import** imports all selected folders marked **Importable**, including any hidden by the search or status filter.

## Template and language

After the file check, you can review the template and language Bearcat chose for each folder and change either before importing.

If you chose a **series template**, Bearcat uses it for folders that meet any of the conditions below. All other folders use the **default release template**.

- The folder contains archives from more than one release.
- The release name matches the episode pattern in the series template's collection settings.
- The release name contains a season number, such as `S01` in `Show.S01.German.1080p-GRP`.

Bearcat reads the **primary language** from the release name. For example, `German` gives `de`. If the name has no language and the folder contains archives from several releases, Bearcat checks the first archive name. If it cannot identify a language, it uses the default language for the import.

## Which mirrors Bearcat imports

Bearcat matches each mirror to an upload configuration for the same hoster.
New releases use the template's configurations. Existing releases use their own configurations.

| Decision | When |
| --- | --- |
| **Adopted** | All files are online and use a supported archive format (`.rar`, `.r00`, `.7z`, `.7z.001`). Additional mirrors must have the same file names as the first adopted mirror. |
| **Bearcat uploads the files** | The hoster is configured in the release, but the mirror does not meet all the conditions above. |
| **Unsupported hoster** | Bearcat does not know the hoster of the links. |
| **Hoster not in the release** | The release has no remaining upload configuration for this hoster. |

With a managed template, Bearcat looks for an unused folder with the release name in the working directories and
the base paths of the folder automations. If it finds one, it uses that folder to create a managed release.
Otherwise, it creates an unmanaged release and restores its archive from the mirrors when needed.

Bearcat also looks for an existing archive in the archive storage folders, working directories, archive base folder
and their direct subfolders. Its file names must match those of the first adopted mirror exactly.
If Bearcat finds a matching archive, it uses it unchanged without downloading anything.
Without a release folder, the release remains unmanaged even if an archive is available.

## Folders that cannot be imported

- No mirror belongs to a hoster of the release.
- No mirror can be adopted and there is no release folder.
- There is no release folder, no existing archive and no archive base folder was selected.

The last two conditions only apply when the import creates a new release.

For [season folders](#season-folders), the import also fails in these cases:

- The release template has no collection detection.
- The episodes do not belong to the same collection.

## Existing releases

Bearcat matches existing releases by name, ignoring case. It uses the folder name or the name you entered during review.

- Folders with the same release name belong to one release, so you can import a separate folder for each hoster.
- If the release already exists, Bearcat adds the folder to it, for example a folder from a second crypter.
- A mirror with the same links as an existing upload is linked to this upload.
- If a hoster already has a newer upload, the container gets that upload's links on its next update.
- A folder's import fails if the release already has a container from the same crypter account for one of its hosters.

## Season folders

A folder can contain several episodes, for example a whole season. Bearcat identifies them by their
archive file names, such as `Show.S01E01...part1.rar` and `Show.S01E02...part1.rar`. If a mirror contains
several archives, Bearcat creates a release for each one, using the archive name without the part number.
During review, you can see the episodes below the folder.

- Set up [collection detection](/Bearcat/release-collections/#how-a-collection-is-created) in the release template. All episodes must be assigned to the same collection.
- Bearcat looks for each episode's local files separately. A folder that contains exactly the archive parts of one episode is used as it is.
- If all episodes are in one folder, Bearcat moves each episode's archive parts into a subfolder named after the episode. The review and the notification at the end show this.
- Bearcat adopts the folder as the container for the collection's [upload slot](/Bearcat/release-collections/#upload-slots). All mirrors in the folder must belong to this slot, or the import fails.
- Bearcat skips mirrors with unknown file names or no archive parts because it cannot assign them to an episode.
- The import fails if the upload slot already has another container of the same crypter account, or if a release in the collection uses this crypter account for its own containers.

If you import a folder with just one episode and its upload slot already uses the same crypter account,
Bearcat imports it without its container. The episode's links go into the collection's container.

## Link crypter containers

- A folder with a single mirror for one of the release's hosters becomes a container for the matching upload configuration (**Each release separately**).
- A folder with several mirrors becomes a **Release (all hosters as mirrors)** container. If the import creates a new release, Bearcat adds links from the remaining hosters once their uploads finish.

See [Link crypter containers](/Bearcat/upload-lifecycle/#5-link-crypter-containers).

## After the import

- For new releases, Bearcat uses the primary language shown in the import preview when fetching release information. Existing releases keep their language.
- After the import, a background task fetches release information (xREL, PreDB, NFO).
- Imported releases count as posted and do not appear in the [post queue](/Bearcat/post-queue/). This also applies to collections created from a season folder.

## Archives not found during the import

If Bearcat cannot find the local archives during import, it marks them as **Deleted**.
Bearcat restores them from the mirrors when needed. If the archive files are already on disk,
you can assign their folders afterwards:

1. Open **Uploads and archives** > **Archives** and select the archives.
2. Click **Change archive folders** and select a base path.

   ![Selected archives with the Change archive folders button](images/link-crypter-import-change-archive-folders-selection.png)

3. Bearcat searches the base path and all subfolders. Review the archive folders it found, then click **Change archive folders** to confirm.

   ![Archive folders found below the base path](images/link-crypter-import-change-archive-folders-dialog.png)

Bearcat must find a single matching folder for each archive, containing exactly that archive's parts.
It then sets the status to **Created** and uses the folder unchanged.
Bearcat skips the archive in these cases:

- The archive is neither **Deleted** nor **Missing files**.
- A newer archive already exists for the archive configuration.
- No matching folder was found, or more than one folder matches.
- The folder also contains another archive.
- The folder is already used by another archive.
- The same folder was found for several selected archives.
- The release is managed and the archive folder is inside its release folder.
