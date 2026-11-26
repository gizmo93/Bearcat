---
title: "WordPress"
description: "Set up WordPress as a distribution site and post releases to your blog."
---

Use Bearcat to prepare drafts in WordPress, publish posts through [posting rules](/automatic-forum-posting/)
and update existing posts. WordPress appears as **Blog** in the list of distribution sites.
Bearcat connects through the WordPress REST API.

## Requirements

- WordPress 5.6 or later. Application passwords were added in that version.
- HTTPS. WordPress only offers application passwords over HTTPS or in a local environment.
- Application passwords enabled. Some security plugins disable them.
- An account with the Editor or Administrator role. **Try login** fails if the account is not allowed
  to publish posts. WordPress filters the HTML of roles below Editor (capability `unfiltered_html`),
  so tags from your template can be stripped.
- No Cloudflare or other bot challenge in front of the site.

Bearcat calls the REST API through `index.php?rest_route=`. Any permalink setting works.

## Create an application password

1. Log into WordPress with the account Bearcat should use.
2. Open **Users > Profile** and scroll to **Application Passwords**.
3. Enter a name, for example `Bearcat`, and create the password.
4. Copy the password. WordPress shows it only once.

## Add the distribution site

Open **Registrations > Distribution site registrations** and add a site. Select **WordPress** and fill in these fields:

| Field | Value |
| --- | --- |
| **Address** | Blog address, for example `https://example.org/`. Include the installation folder if WordPress runs in a subfolder. |
| **Username** | The WordPress username |
| **Application password** | The application password, not the account password |
| **Upload cover as featured image** | Off by default. See [Images](#images). |

Save and click **Try login**.

## Targets

Choose a WordPress category as the **Target**. Bearcat also shows subcategories,
for example `Movies › HD Movies`. Each post and each rule has one category.

## Posting

[Automatic posting](/automatic-forum-posting/) publishes the post directly.
Blogs have no threads or replies, so thread search, prefixes and posting mode do not apply.

With [manual posting](/posting-to-forums/), you prepare a draft and publish it yourself:

1. Click **Prepare draft**. Bearcat creates a draft in WordPress.
2. Click **Open draft**. The link opens the WordPress editor (`/wp-admin/post.php?post=<ID>&action=edit`).
   You must be logged into WordPress in that browser.
3. Publish the post in WordPress.
4. Click **I have posted**. Bearcat stores `<address>/?p=<ID>` under **Posted locations** without searching.

**Thread search without dots** on the **Posting rules** page also applies to blogs. When it is on (default),
Bearcat replaces dots in the post title with spaces.

Updating a post replaces its entire content.

## Existing posts

Before automatic posting, Bearcat searches the blog for posts with the same title. Trashed posts do not count.

| Posts found | Action |
| --- | --- |
| No post | Creates a new post. |
| One published post from your account | Replaces its content and saves the URL under **Posted locations**. |
| One post from your account with a different status, for example a draft | Replaces its content, publishes it and saves the URL under **Posted locations**. |
| One post by another author | Skips the site and leaves the post unchanged. |
| Several posts | Skips the site and leaves the posts unchanged. |

When Bearcat updates an existing post, any manual edits to its content are lost. Its featured image stays unchanged.
A skipped site stays pending and is checked again on every automatic run.

With manual posting, the compose step shows **A post with this title already exists on the site.** with links to the posts.
You can still prepare a draft.

## Template

Create a [post template](/forum-post-templates/) with the output format **HTML**.
Only HTML templates are available when posting to WordPress.

Bearcat sends the rendered template unchanged as the post content. Wrap it in a `wp:html` block so the
block editor keeps the markup, and escape values with `| html.escape`:

```text
<!-- wp:html -->
<div class="release-layout">
  {{~ if imagelinks.cover.full ~}}
  <img src="{{ imagelinks.cover.full | html.escape }}" alt="{{ classification.title | html.escape }}">
  {{~ end ~}}
  {{~ if release.nfo ~}}
  <details>
    <summary>NFO</summary>
    <pre>{{ release.nfo | html.escape }}</pre>
  </details>
  {{~ end ~}}
  {{~ for crypter in link_crypters ~}}
  <a href="{{ crypter.container_link | html.escape }}" rel="nofollow noopener">{{ crypter.name | html.escape }}</a>
  {{~ end ~}}
</div>
<!-- /wp:html -->
```

For a complete example, see
[`tools/wordpress/release.scriban`](https://github.com/gizmo93/Bearcat/blob/main/tools/wordpress/release.scriban)
in the repository.

## Images

Link images from your image hoster in the template with `imagelinks`.

With **Upload cover as featured image** enabled, Bearcat downloads the release or collection cover,
uploads it to the WordPress media library, and sets it as the featured image of the new post or draft.
Without a cover, the post gets no featured image. Bearcat does not upload screenshots to the media library.

- The account must be allowed to upload files. Otherwise **Try login** fails.
- Each new post with a cover adds a separate image to the media library.
- Deleting a post in WordPress does not delete its image.
- Updating a post does not change its featured image.

## Not supported

- Tags
- Several categories per post
