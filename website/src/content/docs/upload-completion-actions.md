---
title: "Actions After Upload"
description: "Run actions such as an IntelliGeN XML export automatically once a release is completely uploaded."
---

Actions after upload run automatically once a release is completely uploaded.
The first available action is the **IntelliGeN XML export**.

## Add an action

1. Open **Automation > Actions after upload** in the sidebar and click **New action**.
2. Enter a name and choose the **Action type**.
3. Choose when the action runs:
   - **Run for first upload** runs once all uploads of a release are completed for the first time.
   - **Run for reupload** runs again once a reupload of a release is completed.
4. Fill in the configuration and click **Save**.

New actions are active. Use **Disable** in the action menu to pause an action.

## Behavior

- Bearcat checks every 5 minutes for releases whose uploads, link containers, and images are complete.
- Each action runs once per completed upload state. A later reupload counts as a new state.
- Actions only run for uploads completed after the action was created. Older releases are not processed.
- A failed action is retried automatically on the next check. Bearcat creates one notification
  for the first failure, not for every retry.

The **Actions after upload** section on the overview tab of the
[release detail page](/Bearcat/release-detail-page/) shows the last result of each active action.
**Run now** runs an action immediately, even when it already ran or its trigger is turned off.
The button is available once all uploads of the release are completed.
A failed manual run creates no notification.

## Release collections

A [release collection](/Bearcat/release-collections/) that uploads via
[upload slots](/Bearcat/release-collections/#upload-slots) is handled as one unit:

- The action runs once all upload slot uploads of all episodes, the collection containers, and the
  collection images are complete, and every release in the collection has passed the
  [quality gate](/Bearcat/quality-gates/).
- It runs again when an episode is added or reuploaded. It counts as a reupload only when all new
  uploads are reuploads.
- `release.name` is the collection name.
- Uploads of a release outside the upload slots are not part of the collection. They still trigger
  the action for the release itself.

The collection detail page has the same **Actions after upload** section with **Run now**.

## IntelliGeN XML export

The export writes one XML file per run that IntelliGeN can import.

| Field | Meaning |
| --- | --- |
| **Export folder** | Folder the XML files are written to. It must already exist. Bearcat never creates it. |
| **File path template** | Path of the file relative to the export folder. It may contain subfolders. |

For a collection, every run writes one new XML file with the links of all episodes. Each upload
slot and hoster becomes one mirror.

Existing files are never overwritten. Bearcat appends a number instead, for example
`Release.Name (2).xml`.

The file path template uses [Scriban](https://github.com/scriban/scriban) syntax.
The default is `{{ release.name }}.xml`. Available variables:

| Variable | Value |
| --- | --- |
| `{{ release.name }}` | Release name |
| `{{ release.title }}` | Title from the metadata |
| `{{ release.content_type }}` | Content type |
| `{{ release.platform }}` | Platform of a game |
| `{{ release.genre }}` | Genre |
| `{{ release.runtime_minutes }}` | Runtime in minutes |
| `{{ release.video_codec }}` | Video codec of the main video |
| `{{ release.audio_codec }}` | Audio codec of the main audio stream |
| `{{ is_reupload }}` | `true` for a reupload, otherwise `false` |

Example that puts reuploads into a subfolder:

```text
{{ if is_reupload }}Reuploads/{{ end }}{{ release.name }}.xml
```

### Import into IntelliGeN

Point IntelliGeN's **DirWatch** at the export folder. IntelliGeN then picks up every new file.

Crypters and dropdown values such as genre, language, or codecs are only applied by IntelliGeN
when the value exists in its own lists. Other values are ignored by IntelliGeN.
