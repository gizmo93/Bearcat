---
title: "Import Existing Link Crypter Folders"
description: "Create releases from existing FileCrypt or Hide.cx folders and keep their links."
---

Bearcat reads the folders of a link crypter account and creates one release per release name.
Mirrors with usable links are adopted as completed uploads. Bearcat uploads the other hosters of the release template itself.

## Supported link crypters

- **FileCrypt**: the API key needs the `LIST` right to list your folders.
- **Hide.cx**

## Steps

1. Open **Crypters** > **Link crypter imports** and click **New import**.
2. Select the crypter registration and the release template. The template defines the hosters, archive settings and link crypter settings of the new releases.
3. Optional: select an **archive base folder for releases without local files**. It is needed for releases without a local release folder: Bearcat restores their archives there from the imported mirrors.
4. Bearcat reads all folders and their links in the background. Folders whose URL Bearcat already knows are skipped.
5. Select one hoster account per hoster and click **Check files**. Bearcat checks the files with this account only.
6. Review the folders, select the ones to import and click **Start import**.

## How mirrors are decided

Each mirror is matched to an upload configuration with the same hoster: of the template for a new release, of the release for an existing release.

| Decision | When |
| --- | --- |
| **Adopted** | All files are online and are archive parts (`.rar`, `.r00`, `.7z`, `.7z.001`). Every further adopted mirror needs the same file names as the first adopted mirror. |
| **Bearcat uploads this hoster itself** | The hoster is in the release, but a condition above is not met. |
| **Unsupported hoster** | Bearcat does not know the hoster of the links. |
| **Hoster not in the release** | The release has no further upload configuration for this hoster. |

With a managed template, Bearcat looks for an unused folder with the release name in the working directories and
the base paths of the folder automations. If it finds one, the release becomes managed with this folder.
Otherwise the release becomes unmanaged and Bearcat restores its archive from the mirrors when it is needed.

## Folders that cannot be imported

- No mirror belongs to a hoster of the release.
- No mirror can be adopted and there is no release folder.
- There is no release folder and no archive base folder was selected.

The last two only apply to folders that create a new release.

## Existing releases

The folder name is the release name (case is ignored).

- Folders with the same release name are combined into one release. One folder per hoster works.
- A folder whose release already exists is attached to this release, for example the folder of a second crypter.
- A mirror with the same links as an existing upload is linked to this upload.
- A hoster that already has a newer upload gets the current links on the next container update.
- A folder fails if the release already has a container of the same crypter account for one of its hosters.

## Link crypter containers

- A folder with one mirror of a release hoster becomes a container of this upload configuration (**Each release separately**).
- A folder with several mirrors becomes one **Release (all hosters as mirrors)** container. If the folder creates the release, hosters that are missing in the folder join this container after their upload.

See [Link crypter containers](/Bearcat/upload-lifecycle/#5-link-crypter-containers).

## After the import

- New releases get the **primary language** of the import. Bearcat uses it for their release information. Existing releases keep their language.
- Release information (xrel, PreDB, NFO) is not fetched during the import. The release information background task fetches it afterwards.
- Imported releases count as posted and do not appear in the [post queue](/Bearcat/post-queue/).
