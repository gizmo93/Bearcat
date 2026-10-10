---
title: "Archive Storage Folders"
description: "Register folders outside the working directories that can hold archives."
---

A storage folder is a folder outside your working directories that can hold archives, for example on a
NAS, a second disk, or a network share. Bearcat treats it as a plain directory and does not check what
is behind it.

## Add a storage folder

In the navigation, open **Configuration > Release settings > Archive storage folders**.
You can also find the page with **Ctrl+K** or **Cmd+K**.

Click **New storage folder** and fill in the fields:

| Field | Meaning |
| --- | --- |
| **Name** | Unique name, up to 100 characters. |
| **Path** | Absolute path of an existing folder, up to 500 characters. Must be unique. |
| **Minimum free space (GB)** | Space that must stay free in the folder after archives were moved there. `0` or more. |
| **Priority** | Lower values are preferred. Several folders can share a priority. |
| **Copy archives back before reupload** | On: archives are copied back to local disk before a reupload. Off: a reupload reads them directly from the storage folder. |

New storage folders are active. Use **Enable** or **Disable** in the row menu to change that.

## Path rules

- The path must be absolute and the folder must already exist. Bearcat never creates a storage folder.
- The path must not be a working directory, lie inside one, or contain one.
- Use **Browse** to pick any folder on the machine running Bearcat.
- In Docker, enter the path inside the container. Make the folder available there through a bind mount.

## Overview

The list shows for each storage folder:

- name and path
- priority
- current free space of the path, or **Unavailable** if the folder cannot be found
- the configured minimum free space
- the number of archives stored there
- whether archives are copied back before a reupload
- whether the folder is active

## Edit or delete a storage folder

The path cannot be changed while archives are stored in the folder. All other fields can be changed
at any time.

A storage folder that still holds archives cannot be deleted. Deleting a storage folder only removes
it from Bearcat. The folder and its contents on disk are kept.
