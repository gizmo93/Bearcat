---
title: "The Release Detail Page"
description: "Find upload progress, download links, metadata, and archive settings for a release."
---

Open **Releases** and click a release name. For a walkthrough, see [Your first upload](/post-installation/).

![release-detail-page.png](images/release-detail-page.png)

## Header

**Post to site** opens the [posting dialog](/posting-to-forums/). The button is highlighted
when the release has at least one online hoster and has not been posted yet. Without an active
distribution site, you see **Render post** instead.

Click **Start uploads now** to skip the [initial upload cooldown](/upload-lifecycle/#2-the-first-upload)
for this release. The button appears if at least one upload configuration has no upload yet.

Use the `...` menu to convert between [release types](/release-types/#converting-between-types).

The status display below the header shows progress. Red marks a problem or an unfinished step.
Click a step to open its tab.

## Overview

![overview.png](images/overview.png)

**Copy all links** copies one link crypter's container links for all hosters.
[Posted locations](/posting-to-forums/#posted-locations) are listed at the bottom.

Under **Image uploads**, each image upload configuration shows its cover and screenshot previews.
Use the copy buttons to copy the image links.

![overview-image-uploads.png](images/overview-image-uploads.png)

## Release info

![release-infos-tab.png](images/release-infos-tab.png)

View and edit the title, description, cover, NFO, and media data here.
Use **Resolve metadata** to search the configured providers again.
See [Release Information and Metadata](/release-information-and-metadata/)
to set up sources or correct a wrong match.

## Archives

![archive-configurations-tab.png](images/archive-configurations-tab.png)

To upload a managed release as an archive, add an archive configuration here. See
[Creating the archive](/upload-lifecycle/#3-creating-the-archive) for the packing options.

Archives in a [storage folder](/archive-storage-folders/) show the storage folder name next to
their state. While a reupload uses a [local working copy](/archive-storage-folders/#reuploads),
the name is followed by **local working copy**.

## Uploads

**Configuration** connects an archive configuration to a hoster account and optional link crypters.

![upload-configurations-tab.png](images/upload-configurations-tab.png)

**History** lists all upload runs, with per-run actions such as starting a manual reupload.

![release-uploads-tab.png](images/release-uploads-tab.png)
![upload-links-dialog.png](images/upload-links-dialog.png)
![crypter-links-dialog.png](images/crypter-links-dialog.png)

## Images

![image-upload-config.png](images/image-upload-config.png)

Cover uploads need a cover URL, for example from TMDB or xREL.
An [image upload configuration](/account-settings/#image-upload-configuration) can also upload
screenshots and the screenshot grid. The **Image uploads** list shows each upload's image type:
cover, screenshot, or screenshot grid.

The **Screenshots** card shows the generated [screenshots](/release-templates-and-automations/#screenshots).
Click an image to open it at full size.

![release-screenshots.png](images/release-screenshots.png)

![image-uploads.png](images/image-uploads.png)
