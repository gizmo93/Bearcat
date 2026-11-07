---
title: "Replace Hosters"
description: "Move upload configurations and templates from one hoster to another account or pool."
---

Open **Hosters** > **Replace hosters**. Choose the hoster, the accounts and pools to replace, and one
replacement account or pool of the same kind (video or archive). The preview updates with every change.
Click **Replace hoster** to apply it. Bearcat cannot undo this automatically.

## What gets replaced

- Upload configurations of releases and in release templates get the replacement as upload target.
- A configuration named like the old account or pool takes the name of the replacement. Other names stay.
- **Premium only** is turned off if the replacement hoster does not support it.
- Uploads that have not started and have no files move to the replacement.

Pools are never changed. Only the upload target of the configuration changes. An unused pool stays and
you can delete it on the [account pools](/Bearcat/account-pools/) page.

Existing uploads stay on the old hoster until their next reupload. The next reupload uses the replacement.

## Skipped configurations

| Case | Result |
|---|---|
| The release or template already has the same archive configuration on the replacement hoster | The configuration stays on the old hoster |
| Several selected accounts use the same archive configuration of a release or template | The first is replaced, the others stay |
| One configuration of a collection upload slot is skipped | All configurations of this slot stay |

The preview lists skipped configurations with a link to the release or template.

## Hoster is dead

A dead hoster usually never reports files as offline, and Bearcat does not check inactive accounts.
Without this option, existing uploads stay online in Bearcat and no reupload starts.

With **Hoster is dead**, Bearcat also does the following:

- Deactivates the selected accounts and all members of the selected pools, even members you did not
  select or that are in other pools.
- Marks completed uploads of these accounts and their files as offline, including uploads of skipped
  configurations.

The option is not available if the replacement is on the same hoster.

[Automatic reuploads](/Bearcat/upload-lifecycle/#7-automatic-reuploads) then run on the replacement after
the usual waiting time. They need **Automatic reuploads** in the release group. Skipped configurations
stay on the deactivated account and get no reupload until you change them.

## What does not change

- Running uploads and uploads that already have files stay on the old hoster. Cancel them if they do
  not finish.
- Links in existing forum posts do not change.
- Hoster names written literally in forum post templates must be changed by hand. `upload.hoster_name`
  follows the latest upload.
