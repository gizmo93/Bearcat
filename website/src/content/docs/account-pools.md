---
title: "Account Pools"
description: "Spread uploads over several accounts of one hoster based on their free storage."
---

An account pool groups several accounts of the same hoster. An upload configuration or a release
template can use a pool as **Upload target** instead of a single account. Bearcat picks the account
when the upload starts.

## Supported hosters

Pools only work with hosters that report the free storage of an account:

- Rapidgator
- Alfafile
- Fast2Share.com
- XFileSharing-based hosters: clicknupload.click, datavaults.co, ddownload, file-upload.org,
  fileq.net, fileserve.com, freedl.ink, hxfile.co, katfile.com, mega4upload.net, uploady.io

## Create a pool

Open **Hosters** > **Account pools** and click **New pool**. Choose the hoster, add the accounts and
drag them into order. The first account is used first. The hoster cannot be changed later.

**Minimum free space** is the space that must stay free on each account after an upload.

## Account selection

When a pool upload starts, Bearcat checks the accounts in pool order and assigns the first one that fits:

- Inactive accounts, accounts with unreadable credentials and accounts waiting for a captcha
  verification are skipped.
- An account fits if its free storage, minus the files still uploading to it and minus the minimum
  free space, is at least the size of the upload.
- Accounts with unlimited storage always fit.
- If the hoster rejects the credentials, Bearcat deactivates the account and tries the next one.
- If the free storage cannot be read, Bearcat skips the account for 5 minutes.

Bearcat caches the free storage of each account for 5 minutes.

## Pool full

If no account fits, the upload stays pending and Bearcat creates an **Account pool full**
notification, once per upload. Bearcat checks the pool again on every upload run and starts the
upload as soon as an account has enough space, for example after you add an account or free up storage.

## Reuploads

A reupload stays on the account of the previous upload if that account is still active and part of
the pool. Bearcat does not check the free storage in this case. Otherwise the reupload gets an
account from the pool when it starts.

## Parallel uploads

The parallel upload limit applies per hoster, not per account. All accounts of a pool share it.
See [Parallel uploads per hoster](/Bearcat/account-settings/#parallel-uploads-per-hoster).
