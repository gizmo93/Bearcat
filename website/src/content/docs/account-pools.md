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
- Zippyshare.day
- XFileSharing-based hosters: clicknupload.click, datavaults.co, ddownload, file-upload.org,
  fileq.net, fileserve.com, freedl.ink, hxfile.co, katfile.com, mega4upload.net, uploady.io

## Create a pool

Open **Registrations > Hosters** > **Account pools** and click **New pool**. Choose the hoster, add the accounts and
drag them into order. Bearcat checks the accounts from top to bottom. You cannot change the hoster later.

Set **Minimum free space** to choose how much storage must stay free on each account after an upload.

## Account selection

When an upload starts, Bearcat checks the accounts in pool order and picks the first with enough space.
These rules apply:

- Inactive accounts, accounts with unreadable credentials, and accounts with a pending captcha are skipped.
- Available space is the account's free storage minus the minimum free space and the remaining files of
  uploads already in progress. That remainder must fit the new upload.
- Accounts with unlimited storage always have enough space.
- If the hoster rejects an account's credentials, Bearcat deactivates it and tries the next one.
- If the free storage check fails, that account is skipped for 5 minutes.

Storage check results are cached for 5 minutes.

## Pool full

If no account has enough space, the upload waits. Bearcat sends an **Account pool full** notification
once per upload. If the pool is empty or Bearcat cannot use any of its accounts, it sends
**Account pool without usable account** instead. Check that the accounts are active, have valid
credentials, and do not have a pending captcha.

Bearcat checks the pool again on each upload run. The upload starts automatically once an account has
enough space. You can add an account or free up storage to make room.

## Reuploads

A reupload reuses the previous account as long as it is still in the pool, active, has readable
credentials, and has no pending captcha. Free storage is not checked for reuploads. Otherwise, Bearcat
picks another account from the pool when the reupload starts, or if the account becomes unusable before
the first file is uploaded.

## Remove an account

You cannot delete an account while it still holds the latest uploads for an upload configuration
targeting a pool or another account, because Bearcat would reupload those releases. Remove the account
from the pool and deactivate it instead. It will receive no new uploads, and its existing uploads stay
intact.

## Replace a pool

To move all upload configurations from a pool to another account or pool, use
[Replace Hosters](/replace-hosters/).

## Posts

In posts, `upload.hoster_name` is the hoster's display name (e.g. "Rapidgator"), not the pool or
account name. `upload.hoster_registration_name` shows the account name of the latest upload.

## Parallel uploads

All accounts of the same hoster share the parallel upload limit.
See [Parallel uploads per hoster](/account-settings/#parallel-uploads-per-hoster).
