---
title: "Group Related Releases into Collections"
description: "Group related releases like a TV season and share their uploads and links."
---

A release collection groups related releases, for example all episodes of a TV show season.

## Why you would use one

For a TV season, you can set the hoster, archive settings and password policy once for all
episodes. A collection can also gather their download links into one link crypter container
that you can share in a forum post.

## How a collection is created

Enable **Collection detection** on a release template to create collections automatically.
Bearcat checks the name of each new release created from that template for a matching collection.

Choose how Bearcat groups releases:

- **Series episode pattern** reads names like `Show.S01E01` and groups everything from the
  same show and season together.
- **Custom regex** lets you describe your own pattern when your names do not follow the usual
  series layout.

When a matching release shows up, Bearcat either creates a new collection or adds the release
to the one that already exists. You can find all of your collections on the **Release
collections** page and open any of them to see the details.

To add a release manually, open a collection and select **Add release**. Only releases from the
same release group can be added.

## Upload slots

Create an upload slot to use the same upload settings for every release in the collection.

When you create a slot you choose:

- A name, such as "Rapidgator passworded".
- The hoster to upload to.
- The archive configuration to use for each release.
- Whether to restrict downloads to premium users, if supported by the hoster.
- A password policy for all releases in the slot.

You can have several slots in one collection, for example one slot per hoster, and each slot
keeps its own settings across all releases.

## Container links

A slot can also create container links through your link crypters. A container link gathers the
download links from every release in the slot into a single link, so one link covers the whole
season instead of one link per episode.

Use **Edit container link crypters** to choose the crypters for a slot. The password and other
crypter settings apply to all uploads in that slot.

## What you find inside a collection

A collection page is built from a few parts: the series metadata, the image uploads for the
series cover, the upload slots, and the releases that belong to it.

![release-collection-detail.png](images/release-collection-detail.png)

### Series metadata

Bearcat uses active metadata sources such as TMDB and TheTVDB to find the series title,
description, and cover. Select **Resolve metadata** to search again after changing the name or language.

The primary language controls translated titles and descriptions. If it is empty, the provider's
default language is used. Bearcat can use an IMDb ID found on one of the collection's releases and
falls back to a title search when no ID is available.

The resolved values are available in forum post templates, and the cover image can be uploaded to
configured image hosters. See [Release Information and Metadata](/Bearcat/release-information-and-metadata/)
for the complete lookup flow.

### Image uploads

**Image uploads** uploads the series cover to your image hosters for use in forum posts.
You can configure this in two places:

- **On a release template:** enable collection detection, then add hosters under **Collection
  image upload configurations**. Bearcat applies them to the collections that receive the template's releases.
- **On a collection:** select **Add** under **Image uploads** and choose a hoster.

If you leave the name empty, Bearcat names the configuration after the hoster. You can rename
an existing entry. To use a different hoster, add a new image upload configuration.

Once the cover has been uploaded, each entry shows its state and the image links, grouped by
size. You can copy a single link or all of them at once. These same links are available in a
collection forum post template through `imagelinks`, just like they are for a single release.
See [Forum post templates](/Bearcat/forum-post-templates/) for the details.

### Releases

View each release's upload links or remove releases from the collection.
