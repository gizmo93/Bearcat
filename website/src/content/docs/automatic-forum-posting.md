---
title: "Post Releases to Forums Automatically"
description: "Enable and configure rule based automatic forum posting"
---

Posting rules let Bearcat choose a subforum, fill in a template and submit the post.
Run them with **Post** in the [post queue](/Bearcat/post-queue/) or enable automatic posting.
To review and submit drafts yourself, use [Posting to Forums](/Bearcat/posting-to-forums/).

Each forum has its own rules. The first matching rule wins. If none matches, Bearcat skips that forum.

## What you need first

- A forum added under **Distribution sites**, with a working login.
- At least one [forum post template](/Bearcat/forum-post-templates/) that the rule can render.
- Releases that reach the [post queue](/Bearcat/post-queue/), pass their
  [quality gate](/Bearcat/quality-gates/) and have a classification. Releases in groups without
  a quality profile pass the gate automatically.

## Opening the posting rules page

Open **Distribution sites** in the sidebar. The table has a **Posting rules** column that shows how
many rules a forum has. 

![distribution-site-rules.png](images/distribution-site-rules.png)

Click the number to open the page, or use **Posting rules** in the row menu.

![posting-rules-page.png](images/posting-rules-page.png)

## Site-wide settings

Two switches at the top of the page apply to the whole forum registration.

**Automatic posting** is off by default. You can still preview rules and use **Post** in the post queue.
When enabled, the "Automatic forum posting" background task posts matching releases in the queue every 15 minutes by default.
Change the interval under [Background tasks](/Bearcat/advanced-configuration/#background-tasks).

**Thread search without dots** is on by default. Bearcat replaces dots with spaces when searching
for threads and naming new ones. Turn it off to use the dotted release name for both.

## Adding a rule

Click **New posting rule**. A rule has these parts:

- **Name**: your own name for that rule
- **Subforum**: where a matching release is posted. Click **Load subforums** first. Bearcat signs in
  to the forum and reads the current forum tree, which can take a few seconds. The stored subforum
  stays selected until you pick a different one, so you do not have to load the list to edit the
  other fields.
- **Prefix**: some subforums allow you to set prefixes on new threads
- **Forum post template**: the template that is rendered as the post body.
- **Posting mode**: either *Reply to an existing thread, otherwise start a new one*, or *Always start
  a new thread*.
- **Enabled**: disabled rules are skipped while matching

![add-or-edit-rule.png](images/add-or-edit-rule.png)

## Conditions

A rule applies to a release when its condition matches. A condition is built from comparison rows
inside groups:

- **All (AND)**: every entry in the group has to match.
- **Any (OR)**: at least one entry has to match.
- **NOT**: matches when the single condition inside does not match.

Groups can be nested, so you can build something like "German and (1080p or 2160p) and not from
group X". Use **Condition**, **Group** and **NOT** inside a group to add entries.

Each comparison row picks a field, an operator and a value:

| Field | Values |
| --- | --- |
| Release name | Free text |
| Resolution | From the classification, for example `R1080p` or `R2160p` |
| Primary language | Language of the release, for example `German` |
| Multi-language | Yes or no |
| Content type | `Movie`, `TvShowEpisode` or `Other` |
| Source | For example `BluRay` or `WebDl` |
| Release group | The Bearcat release group the release belongs to |
| Release group tag | The group tag from the release name, for example `FLAME` |
| Year | Number |
| Season | Number |
| Episode | Number |

The available operators depend on the field:

| Field | Operators |
| --- | --- |
| Release name | *is*, *is not*, pattern matching and *matches regex* |
| Release group, release group tag | *is*, *is not*, pattern matching, *is one of*, *is none of*, *is set*, *is not set* |
| Primary language | *is*, *is not*, *is one of*, *is none of*, *is set*, *is not set* |
| Resolution, year, season, episode | Include *is at least* and *is at most* |

With *matches pattern (%)*, `%` stands for any text. The pattern must match the whole value:

- `%German%` matches names containing `German`.
- `%-FLAME` matches names ending in `-FLAME`.

Patterns and regular expressions ignore case. An invalid or slow regular expression counts as
no match instead of failing the rule.

Most fields come from the release classification, which Bearcat derives from the release name and,
where available, the media info. Releases without a classification are not posted at all, see below.

## Ordering the rules

The rule list is ordered and the first matching rule wins. Drag a rule by the handle on the left to
move it. Put specific rules above general ones.

## Dry run

Save your rules, then click **Preview** in the **Dry run** card at the bottom of the page.
It checks recent releases and shows the matching rule and subforum, or **No match**, without posting.

Run a preview after changing conditions or rule order, before enabling automatic posting.

![rule-dry-run.png](images/rule-dry-run.png)

## Posting from the post queue

Every entry under **Single releases** in the [post queue](/Bearcat/post-queue/) has an **Automatic
posting** section that shows, per forum, what the rules would do:

- the matched rule and its subforum, with a **Post** button,
- **Posted** with a link, if the release was already posted to that forum,
- **No matching posting rule**, if no rule matches the release.

A forum with automatic posting switched on has an **Auto** badge, so you can tell which entries
the background task will handle on its own.

**Post** renders the template, searches for an existing thread if the rule
says so, replies there or creates a new thread with the prefix, and stores the resulting URL under
**Posted locations** on the release. When more than one forum is still open, **Post to all matched
sites** goes through them one after another.

![posting-from-post-queue.png](images/posting-from-post-queue.png)

Once no matched forum is left open, Bearcat marks the release as posted and it leaves the queue. If
you post somewhere else by hand, **Mark as posted** still works as before.

Collections currently have no automatic posting section. Rules match single releases.

## When a release cannot be posted

Instead of the forum list, the entry shows a short reason:

- **Automatic posting is blocked: the quality gate has not passed.** The release has to be
  [passed or manually approved](/Bearcat/quality-gates/). A release that has not been evaluated yet
  is picked up by the quality gate background task within one interval.
- **Automatic posting is blocked: the release has no classification.** Classification runs in the
  background every 30 minutes and after media metadata are extracted. Wait for the next run, or
  extract the media metadata on the release to trigger it.
- **No forum has posting rules yet.** No forum registration has an enabled rule.

The background task uses the same checks, so a blocked release is skipped there as well and picked
up again once it is ready.

## Updating a post after a reupload

After a reupload, Bearcat can update existing posts with the new download links.
It renders the template again and **replaces the entire post, including any edits you made in the forum**.
This is useful for posts containing direct download links instead of a link crypter container.

### Updating from the queue

A reupload puts the release back in the post queue. Previously used forums show
**Post content is outdated** next to the stored link. Click **Update post** to replace the post.
Bearcat records the post URL, template and update time under **Posted locations**.

For forums with **Automatic posting** enabled, the background task updates posts automatically
if a template is available. The release leaves the queue once all posts and updates are complete.

### Choosing a template

Bearcat uses the first available template in this order:

1. The template you pick in the dialog.
2. The template the post was created with.
3. The template of the rule that currently matches the release.

Older posts may have no stored template. Choose one in the dialog or make sure a posting rule matches.

### Updating from posted locations

You can also update a post from **Posted locations** on a release or collection. The update button
is available for entries linked to a distribution site, including posts you confirmed by hand.
A dialog asks you to confirm the replacement.

Collections can only be updated this way. The background task handles single releases.

## Notifications

Every automatic post creates a notification, whether it worked or not. Updated posts and failed
updates have their own notification kinds, so you can switch them off separately.

## Posted locations and duplicates

Bearcat records each forum in **Posted locations** and shows **Posted** instead of offering to post again.
The background task skips those forums, even if the rules have changed.
