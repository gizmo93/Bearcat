---
title: "Video Hosters"
description: "Upload videos directly to DoodStream, MixDrop, Playmate, Videro, or Vixeo without packing them first."
---

Bearcat can upload a release's main video file directly to a video hoster without packing it first.
Choose **Video file** as the **Upload source** in the upload configuration.

After the upload, you get a file link and an embed link. Use the embed link to add the video player to
another page.

## Supported video hosters

- DoodStream
- MixDrop
- Playmate
- Videro
- Vixeo

## Requirements

Video uploads require a [managed release](/Bearcat/release-types/) with the content type **Movie** or
**TV show episode**. Release templates must also be managed and use one of these content types.

## Main video

Bearcat uses the extracted media data to select the largest file with a video stream and uploads only
that file. If the media data is missing or no file has a video stream, the upload fails.

## Create a video upload configuration

1. Open the release detail page and add an upload configuration.
2. Choose **Video file** under **Upload source**. The field only appears when the release meets the
   requirements for video uploads.
3. Choose an account or [account pool](/Bearcat/account-pools/) of a video hoster as **Upload target**.

With **Video file**, you do not need an archive configuration. The upload targets only include video
hoster accounts and pools. With **Archive files**, you only see the other hosters.
You cannot change the upload source later.

## Release templates

In the [release template](/Bearcat/release-templates-and-automations/), add an upload configuration
template and choose **Video file** as the upload source. All releases created from this template inherit
the video upload configuration, including those from folder automations and FTP downloads.

## Limits

- Video uploads do not support link crypters. Templates with the upload source **Video file** therefore
  have no link crypter templates to choose from.
- Video upload configurations cannot be assigned to [release collection](/Bearcat/release-collections/)
  upload slots. The collection slot fields are also absent from these templates.

## Release folder

Bearcat needs the main video in the release folder for video uploads. While the release has a video
upload configuration:

- Bearcat keeps the release folder. The release is not
  [automatically converted to unmanaged](/Bearcat/advanced-configuration/#convert-releases-to-unmanaged-automatically),
  and its [raw files are kept after remote downloads](/Bearcat/remote-downloads/#delete-raw-files-after-uploading).
- You cannot manually **convert to unmanaged** either.
- The content type must stay **Movie** or **TV show episode**.

Release templates with video uploads must also keep the content type **Movie** or **TV show episode**.

## Uploads, online checks and reuploads

Video uploads go straight to the upload queue without creating an archive. Online checks and reuploads
work the same way as for archive uploads. See [Upload lifecycle](/Bearcat/upload-lifecycle/).

## Links

You can find the file name, file link, and **Embed link** on the release detail page and in the upload
links dialog. You can copy both links. The [REST API](/Bearcat/rest-api/) returns the embed link in
`hosterEmbedLink`.

In the [post queue](/Bearcat/post-queue/), video uploads appear under their upload configuration's name
and are marked **Video**.

Use `upload.is_video_upload` and `upload.embed_links` in
[forum post templates](/Bearcat/forum-post-templates/#video-uploads).

## DoodStream

The file link and embed link use doodstream.com, which redirects to DoodStream's current player domain.
Bearcat moves each upload into its own folder.

## Playmate

Playmate has no separate file page. The file link and embed link use the same video player URL.
Bearcat moves each upload into its own folder.

## Videro

The file link and embed link use videro.my. Videro has no folders, so uploads stay in the account root.

## Vixeo

Vixeo has no separate file page. The file link and embed link use the same video player URL.
