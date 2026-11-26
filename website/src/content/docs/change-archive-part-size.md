---
title: "Change Part Size"
description: "Change the part size of many archive configurations and release templates at once."
---

Open **Uploads and archives > Archives** and click **Change part size**. Select the current part size, enter the
new one, and click **Change part size**. The preview updates with every change.

The part size uses the global [part size unit](/advanced-configuration/#part-size-unit).

## What changes

- Every archive configuration with exactly the selected part size gets the new part size.
- With **Also change release templates** (default: on), archive configurations in release templates
  change as well, so new releases use the new part size.

Only archives packed from now on use the new part size. Existing archives and uploads keep their
part size until they are packed again.

## Skipped configurations

The new part size must fit every target hoster of an archive. If it exceeds any hoster's file size limit,
Bearcat skips that archive configuration. This also applies to release templates.

The preview lists each skipped configuration with the hoster, its limit, and a link to the release or
release template.
