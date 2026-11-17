---
title: "FileFactory: Parallel uploads"
description: "FileFactory can block uploads when more than five run at once."
---

FileFactory can block uploads when more than five run at once.
Bearcat defaults to three parallel uploads for FileFactory.

If you raised the limit and FileFactory blocks uploads, restore the default.
Open **Hoster registrations**, select **Edit** for FileFactory, and clear the
[**Maximum parallel uploads**](/Bearcat/account-settings/#parallel-uploads-per-hoster) field.
Save the change.
