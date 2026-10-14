---
title: "Account Pools"
description: "Spread uploads across several accounts of the same hoster with enough free storage."
---

An account pool groups several accounts of the same hoster. Choose the pool as **Upload target** in
an upload configuration or release template. When the upload starts, Bearcat picks an account with
enough free storage.

## Supported hosters

Pools only work with hosters that report the free storage of an account:

- Rapidgator
- Alfafile
- Fast2Share.com
- XFileSharing-based hosters: clicknupload.click, datavaults.co, ddownload, file-upload.org,
  fileq.net, fileserve.com, freedl.ink, hxfile.co, katfile.com, mega4upload.net, uploady.io

## Create a pool

Open **Hosters** > **Account pools** and click **New pool**. Choose the hoster, add the accounts and
drag them into order. Bearcat checks the accounts from top to bottom. You cannot change the hoster later.

Set **Minimum free space** to choose how much storage must stay free on each account after an upload.

## Account selection

When an upload starts, Bearcat checks the accounts in pool order and picks the first with enough space.
These rules apply:

- Bearcat skips inactive accounts and accounts with unreadable credentials or a pending captcha.
- Bearcat subtracts the space needed for the remaining files of uploads in progress and the minimum
  free space from the account's free storage. The rest must be enough for the new upload.
- Accounts with unlimited storage always have enough space.
- If the hoster rejects the credentials, Bearcat deactivates the account and tries the next one.
- If Bearcat cannot check the free storage, it skips the account for 5 minutes.

Bearcat keeps the result of each storage check for 5 minutes.

## Pool full

If no account has enough space, the upload waits. Bearcat sends an **Account pool full** notification
once per upload.

Bearcat checks the pool again on each upload run. The upload starts automatically once an account has
enough space. You can add an account or free up storage to make room.

## Reuploads

A reupload uses the previous account as long as it is active and still in the pool. Bearcat does not
check its free storage. Otherwise, Bearcat picks another account from the pool when the reupload starts.

## Parallel uploads

All accounts of the same hoster share the parallel upload limit.
See [Parallel uploads per hoster](/Bearcat/account-settings/#parallel-uploads-per-hoster).
