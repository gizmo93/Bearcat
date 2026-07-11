---
title: "Release Information and Metadata"
description: "Configure metadata sources, choose a primary language, and understand how Bearcat resolves release data."
---

Bearcat stores three kinds of information for a release:

- **Release information** describes the scene release. It can contain the release name, size,
  video and audio type, release database links, and other scene-specific details.
- **Metadata** describes the movie or TV show. It can contain the title, genre, description,
  cover image, and a link to the metadata provider.
- **Media data** is read from the local video files with MediaInfo. It contains codecs,
  resolution, duration, audio tracks, and subtitle tracks.

These values can come from different sources. For example, xREL can provide the scene release
information while TMDB provides the title, description, genre, and a higher quality cover image.

## Configure the sources

Open **NFO database registrations** in the sidebar to enable sources for scene release
information and NFO files:

- **xREL** has good coverage for German scene releases.
- **SRRDB** also covers many English and international scene releases and can provide NFO files
  and IMDb IDs.

xREL and SRRDB do not require account credentials.

Open **Metadata sources** to register providers for movie and TV metadata:

- **The Movie Database (TMDB)** supports movies, TV series, and TV episodes.
- **TheTVDB** supports TV series.

Both metadata providers require an API key. Use **Try login** after saving a registration to
check the key.

Bearcat checks active providers in its built-in order and uses the first matching result. A
metadata provider cover is preferred over a scene database cover when it is available. Values
already obtained from the scene database remain as fallbacks when the metadata provider does not
return them.

## Primary language

Releases and release collections can have an optional primary language. Bearcat passes this
language to metadata providers for translated titles and descriptions.

Choose the language explicitly when you create or edit a release or collection. A release folder
automation can also define the language assigned to every release it creates. This works well with
folder patterns such as `*.GERMAN.*` when separate automations handle different languages.

Bearcat does not infer a language from release names. If the language is empty, it stays empty and
the metadata provider uses its own default language. Existing releases and automations therefore
continue to work after an upgrade without receiving an assumed language.

## How resolution works

For a release, Bearcat performs these steps:

1. It reads an existing `.nfo` file from the release folder. If none exists, it asks active NFO
   databases for one.
2. It asks active NFO databases for scene release information.
3. It extracts IMDb IDs from NFO content and results returned by xREL or SRRDB.
4. It asks active metadata sources for movie or TV information. An IMDb ID is used first. If no
   suitable ID is known, Bearcat searches by title and other information parsed from the release
   name.
5. It passes the release's primary language to the metadata provider.

The same metadata resolver is used for release collections. Collections are resolved as TV
series. Bearcat uses an IMDb ID from one of the collection's releases when available, otherwise it
searches by the collection name.

The **Release info resolution** background task handles missing data automatically. You can also
trigger the relevant actions from the **Release Info** tab.

## The Release Info tab

The tab displays metadata and scene release information separately. The visible actions are:

- **Refresh** reloads the current data from Bearcat's database.
- **Resolve metadata** asks the active metadata sources again.
- **Edit release info** opens the manual editor.
- **Resolve now** is shown when no scene release information exists.
- **Add NFO** is shown when no NFO is stored.

The `...` menu contains less common actions such as editing an existing NFO, extracting media
data, and deleting the resolved release information and metadata.

External IMDb IDs are shown together with their sources. If the same ID was found in the NFO and
by xREL, it appears once with both source badges.

## Manual fallback

If no provider finds a match, use **Edit release info** to enter the values manually. The dialog
also accepts an IMDb ID or IMDb URL. A manually assigned IMDb ID is stored separately from IDs
found in the NFO, xREL, or SRRDB.

Use **Add NFO** or **Edit NFO** to enter NFO content manually. Bearcat scans the saved content for
IMDb IDs every time it changes. For managed releases, the edited NFO is also written to the release
folder.

Metadata entered manually is not overwritten by an automatic metadata refresh. If you want to
start over, use **Delete release info and metadata** and resolve the release again. The stored NFO
and extracted external IDs remain available for the next lookup.

