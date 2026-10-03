---
title: "The Release Detail Page"
description: "Find upload progress, download links, metadata, and archive settings for a release."
---

Open **Releases** and click a release name to see these tabs. For a walkthrough, see [Your first upload](/Bearcat/post-installation/).

![release-detail-page.png](images/release-detail-page.png)

## Overview tab

Shows the latest uploads, download and container links, image links, and archive passwords.
If the release folder contains a `.nfo` file, you can copy it here too.
Use this tab to copy the links and details for a forum post.

![overview.png](images/overview.png)

## Release info tab

This tab separates scene release information from movie or TV metadata. It also shows the NFO,
external IDs with their sources, and technical media data. You can resolve, refresh, or edit these
values manually.

![release-infos-tab.png](images/release-infos-tab.png)

See [Release Information and Metadata](/Bearcat/release-information-and-metadata/) for details.

## Archive configurations tab

Sets the archiver, output folder, part size, password, and packing options for each set of archives.
See [Creating the archive](/Bearcat/upload-lifecycle/#3-creating-the-archive) for the packing options.
Add at least one configuration for a managed release.

![archive-configurations-tab.png](images/archive-configurations-tab.png)

## Upload configurations tab

Connects an archive configuration to a hoster account. You can also add link crypters to create containers for the upload links.

![upload-configurations-tab.png](images/upload-configurations-tab.png)

## Uploads tab

Lists upload runs and their links. You can create manual reuploads, cancel running uploads,
or delete finished and failed entries.

![release-uploads-tab.png](images/release-uploads-tab.png)
![upload-links-dialog.png](images/upload-links-dialog.png)
![crypter-links-dialog.png](images/crypter-links-dialog.png)

## Image upload configurations tab

Selects the image hoster accounts that receive the release cover. These are separate from file upload configurations.

![image-upload-config.png](images/image-upload-config.png)

The release needs a cover URL before Bearcat can upload an image. It normally comes from a metadata
source such as TMDB, with the xREL cover as a fallback. No cover means no image upload or image links.

## Image uploads tab

Lists image upload runs and the URLs returned by the image hoster.

![image-uploads.png](images/image-uploads.png)

Depending on the image hoster, Bearcat may receive different image sizes, for example full size, medium size or thumbnail.
You can also copy these links from the **Overview** tab.
