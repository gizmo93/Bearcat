---
title: "Video Hosters"
description: "Upload the main video file of a release to a video hoster and get a file link and an embed link."
---

A video hoster takes a single video file and returns a file link and an embed link. The embed link
embeds a player for the video.

With the upload source **Video file**, an upload configuration uploads the release's main video file
directly to a video hoster. No archive parts are created or uploaded.

## Supported video hosters

- MixDrop

## Requirements

- Only [managed releases](/Bearcat/release-types/) with the content type **Movie** or **TV show episode**.
- In release templates: only managed templates with the content type **Movie** or **TV show episode**.

## Main video

Bearcat uploads only the main video: the largest media file with a video stream, taken from the
extracted media data. If no media data was extracted or no file has a video stream, the upload fails
with an error.

## Create a video upload configuration

1. Open the release detail page and add an upload configuration.
2. Choose **Video file** under **Upload source**. The field only appears when the release allows video
   uploads.
3. Choose an account or [account pool](/Bearcat/account-pools/) of a video hoster as **Upload target**.

A video upload configuration has no archive configuration. With **Video file**, the upload target list
only shows video hoster accounts and pools. With **Archive files**, it only shows the other hosters.
You cannot change the upload source after creation.

## Release templates

In a [release template](/Bearcat/release-templates-and-automations/), add an upload configuration template
with the upload source **Video file**. Releases created from that template get a video upload
configuration. This applies to manually created releases and to releases from folder automations or
FTP downloads.

## Limits

- Video upload configurations do not support link crypters. Upload configuration templates with the upload source **Video file** have no link crypter templates.
- Video upload configurations cannot be used in [release collection](/Bearcat/release-collections/)
  upload slots. Upload configuration templates with the upload source **Video file** have no collection slot fields.
- The upload source is fixed after creation.

## Release folder

Video uploads read the main video from the release folder. While a release has a video upload
configuration:

- Bearcat keeps the release folder. [Automatic conversion to unmanaged](/Bearcat/advanced-configuration/#convert-releases-to-unmanaged-automatically)
  and [raw file cleanup of remote downloads](/Bearcat/remote-downloads/#delete-raw-files-after-uploading) skip the release.
- **Convert to unmanaged** is rejected.
- The content type must stay **Movie** or **TV show episode**.

The content type of a release template with an upload configuration template with the upload source **Video file** must also stay
**Movie** or **TV show episode**.

## Uploads, online checks and reuploads

Video uploads skip archive creation and go straight to the upload queue. See
[Upload lifecycle](/Bearcat/upload-lifecycle/). Online checks and reuploads work like for archive uploads.

## Links

The release detail page and the upload links dialog show the video file name, the link and the
**Embed link**. Both links can be copied. In the [REST API](/Bearcat/rest-api/), the upload link response
contains `hosterEmbedLink`.

In the [post queue](/Bearcat/post-queue/), video uploads are grouped under the name of their upload
configuration with a **Video** marker.

[Forum post templates](/Bearcat/forum-post-templates/#video-uploads) provide `upload.is_video_upload`
and `upload.embed_links`.

## MixDrop

Add a MixDrop account with these fields:

| Field | Value |
| --- | --- |
| **EmailAddress** | The email address of your MixDrop account. |
| **ApiKey** | The API key from the API settings of your MixDrop account. |
