using System.Net;
using System.Text;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.DistributionSites.Shared.XenForo.Api;
using Moq;
using Shouldly;

namespace Bearcat.DistributionSites.UnitTest.Shared.XenForo.Api;

public class XenForoForumClientTest
{
    [Test]
    public async Task SearchThreadsAsync_WithForumUrl_SearchesPostsInSelectedNode()
    {
        // Arrange
        string? requestBody = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse("<html data-csrf=\"token\"></html>"));
        handler.Enqueue(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return HtmlResponse("<html></html>");
        });

        using var client = CreateClient(handler);

        // Act
        await client.SearchThreadsAsync(
            "Ich Weiss Was Du Letzten Sommer Getan Hast",
            "https://www.data-load.me/forums/uhd-4k.9/",
            CancellationToken.None
        );

        // Assert
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("search_type=post");
        requestBody.ShouldContain("c%5Bnodes%5D%5B0%5D=9");
    }

    [Test]
    public async Task SearchThreadsAsync_SeveralResults_OrdersThreadsByThreadIdAscending()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse("<html data-csrf=\"token\"></html>"));
        handler.Enqueue(_ =>
            HtmlResponse(
                """
                <html>
                    <div class="contentRow-title"><a href="/threads/newest.300/">Newest</a></div>
                    <div class="contentRow-title"><a href="/threads/unparsable/">Unparsable</a></div>
                    <div class="contentRow-title"><a href="/threads/oldest.12/post-98">Oldest</a></div>
                    <div class="contentRow-title"><a href="/threads/middle.150/">Middle</a></div>
                </html>
                """
            )
        );

        using var client = CreateClient(handler);

        // Act
        var threads = await client.SearchThreadsAsync(
            "Some Movie",
            "https://www.data-load.me/forums/uhd-4k.9/",
            CancellationToken.None
        );

        // Assert
        threads
            .Select(thread => thread.Title)
            .ShouldBe(["Oldest", "Middle", "Newest", "Unparsable"]);
    }

    [Test]
    public async Task GetForumTreeAsync_ForumWithNodeIdInUrl_ExposesNumericNodeId()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ =>
            HtmlResponse(
                """
                <html>
                    <div class="node node--category">
                        <div class="node-title"><a href="/forums/movies.4/">Movies</a></div>
                    </div>
                    <div class="node node--forum">
                        <div class="node-title"><a href="/forums/uhd-4k.9/">UHD 4K</a></div>
                    </div>
                    <div class="node node--forum">
                        <div class="node-title"><a href="/pages/rules/">Rules</a></div>
                    </div>
                </html>
                """
            )
        );

        using var client = CreateClient(handler);

        // Act
        var tree = await client.GetForumTreeAsync(CancellationToken.None);

        // Assert
        var category = tree.ShouldHaveSingleItem();
        category.NumericNodeId.ShouldBe("4");
        category.Children.Count.ShouldBe(2);
        category.Children[0].NumericNodeId.ShouldBe("9");
        category.Children[1].NumericNodeId.ShouldBeNull();
    }

    [Test]
    public async Task SubmitNewThreadAsync_FormAccepted_PostsAllFieldsAndReturnsRedirect()
    {
        // Arrange
        string? requestBody = null;
        string? requestUri = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(NewThreadPage));
        handler.Enqueue(async request =>
        {
            requestUri = request.RequestUri?.ToString();
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                {"status":"ok","redirect":"/threads/my-release.42/"}
                """
            );
        });

        using var client = CreateClient(handler);

        // Act
        var submitted = await client.SubmitNewThreadAsync(
            forumUrl: "https://www.data-load.me/forums/uhd-4k.9",
            title: "My Release",
            prefixIds: ["3", "7"],
            message: "Body text",
            cancellationToken: CancellationToken.None
        );

        // Assert
        submitted.Url.ShouldBe("https://www.data-load.me/threads/my-release.42/");
        requestUri.ShouldBe("https://www.data-load.me/forums/uhd-4k.9/post-thread");
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("_xfToken=form-token");
        requestBody.ShouldContain("attachment_hash=hash-123");
        requestBody.ShouldContain("last_date=1700000000");
        requestBody.ShouldContain("kzOb4=");
        requestBody.ShouldContain("title=My+Release");
        requestBody.ShouldContain("message=Body+text");
        requestBody.ShouldContain("prefix_id%5B%5D=3");
        requestBody.ShouldContain("prefix_id%5B%5D=7");
        requestBody.ShouldContain("_xfResponseType=json");
        requestBody.ShouldNotContain("_xfResponseType=html");
        requestBody.ShouldNotContain("title=Stale+Draft");
    }

    [Test]
    public async Task SubmitNewThreadAsync_ResponseWithErrors_ThrowsWithPlainTextErrors()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(NewThreadPage));
        handler.Enqueue(_ =>
            JsonResponse(
                """
                {"status":"error","errors":["Please enter a <b>title</b>.","Flood control is active."]}
                """
            )
        );

        using var client = CreateClient(handler);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            client.SubmitNewThreadAsync(
                forumUrl: "https://www.data-load.me/forums/uhd-4k.9/",
                title: string.Empty,
                prefixIds: [],
                message: "Body text",
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("Please enter a title.");
        exception.Message.ShouldContain("Flood control is active.");
        exception.Message.ShouldNotContain("<b>");
    }

    [Test]
    public async Task SubmitNewThreadAsync_PageWithoutForm_ThrowsDescriptiveException()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ =>
            HtmlResponse("<html data-csrf=\"token\"><div>You may not post here.</div></html>")
        );

        using var client = CreateClient(handler);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            client.SubmitNewThreadAsync(
                forumUrl: "https://www.data-load.me/forums/uhd-4k.9/",
                title: "My Release",
                prefixIds: [],
                message: "Body text",
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("post-thread");
        exception.Message.ShouldContain("https://www.data-load.me/forums/uhd-4k.9/post-thread");
    }

    [Test]
    public async Task SubmitReplyAsync_FormAccepted_PostsHiddenFieldsAndReturnsRedirect()
    {
        // Arrange
        string? requestBody = null;
        string? requestUri = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(ThreadPage));
        handler.Enqueue(async request =>
        {
            requestUri = request.RequestUri?.ToString();
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                {"status":"ok","redirect":"https://www.data-load.me/threads/my-release.42/post-99"}
                """
            );
        });

        using var client = CreateClient(handler);

        // Act
        var submitted = await client.SubmitReplyAsync(
            threadUrl: "https://www.data-load.me/threads/my-release.42/",
            message: "Reupped",
            cancellationToken: CancellationToken.None
        );

        // Assert
        submitted.Url.ShouldBe("https://www.data-load.me/threads/my-release.42/post-99");
        requestUri.ShouldBe("https://www.data-load.me/threads/my-release.42/add-reply");
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("_xfToken=form-token");
        requestBody.ShouldContain("attachment_hash=hash-456");
        requestBody.ShouldContain("message=Reupped");
        requestBody.ShouldContain("_xfResponseType=json");
        requestBody.ShouldNotContain("message=Stale+Draft");
    }

    [Test]
    public async Task SubmitReplyAsync_ResponseWithErrors_ThrowsWithPlainTextErrors()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(ThreadPage));
        handler.Enqueue(_ =>
            JsonResponse(
                """
                {"status":"error","errors":{"message":"This thread is closed."}}
                """
            )
        );

        using var client = CreateClient(handler);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            client.SubmitReplyAsync(
                threadUrl: "https://www.data-load.me/threads/my-release.42/",
                message: "Reupped",
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("This thread is closed.");
    }

    [Test]
    public async Task EditPostAsync_FirstPostOfThread_KeepsTitleAndPrefixAndOverridesMessage()
    {
        // Arrange
        string? requestBody = null;
        string? requestUri = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(EditPostPage));
        handler.Enqueue(async request =>
        {
            requestUri = request.RequestUri?.ToString();
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                {"status":"ok","redirect":"https://www.data-load.me/threads/my-release.42/post-99"}
                """
            );
        });

        using var client = CreateClient(handler);

        // Act
        var submitted = await client.EditPostAsync(
            postedUrl: "https://www.data-load.me/threads/my-release.42/post-99",
            message: "Reupped body",
            cancellationToken: CancellationToken.None
        );

        // Assert
        submitted.Url.ShouldBe("https://www.data-load.me/threads/my-release.42/post-99");
        requestUri.ShouldBe("https://www.data-load.me/posts/99/edit");
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("title=My+Release");
        requestBody.ShouldContain("prefix_id=7");
        requestBody.ShouldContain("tags=action%2Cthriller");
        requestBody.ShouldContain("_xfToken=form-token");
        requestBody.ShouldContain("attachment_hash=hash-789");
        requestBody.ShouldContain("message=Reupped+body");
        requestBody.ShouldContain("_xfResponseType=json");
        requestBody.ShouldNotContain("message=Old+body");
        requestBody.ShouldNotContain("_xfResponseType=html");
        requestBody.ShouldNotContain("silent");
        requestBody.ShouldNotContain("save");
    }

    [Test]
    public async Task EditPostAsync_MultiSelectAndCheckedBoxes_SubmitsEverySelectedValue()
    {
        // Arrange
        string? requestBody = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(EditPostWithMultiSelectPage));
        handler.Enqueue(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                {"status":"ok","redirect":"/threads/my-release.42/post-99"}
                """
            );
        });

        using var client = CreateClient(handler);

        // Act
        await client.EditPostAsync(
            postedUrl: "https://www.data-load.me/posts/99/",
            message: "Reupped body",
            cancellationToken: CancellationToken.None
        );

        // Assert
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("prefix_id%5B%5D=3");
        requestBody.ShouldContain("prefix_id%5B%5D=7");
        requestBody.ShouldContain("sticky=1");
        requestBody.ShouldNotContain("lock");
    }

    [Test]
    public async Task EditPostAsync_ThreadUrlWithoutPostId_EditsTheFirstPostOfTheLoggedInUser()
    {
        // Arrange
        string? requestUri = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(LoggedInHomePage));
        handler.Enqueue(_ => HtmlResponse(ThreadWithPostsPage));
        handler.Enqueue(_ => HtmlResponse(EditPostPage));
        handler.Enqueue(request =>
        {
            requestUri = request.RequestUri?.ToString();
            return JsonResponse(
                """
                {"status":"ok","redirect":"/threads/my-release.42/post-99"}
                """
            );
        });

        using var client = CreateClient(handler);

        // Act
        await client.EditPostAsync(
            postedUrl: "https://www.data-load.me/threads/my-release.42/",
            message: "Reupped body",
            cancellationToken: CancellationToken.None
        );

        // Assert
        requestUri.ShouldBe("https://www.data-load.me/posts/99/edit");
    }

    [Test]
    public async Task GetForumTreeAsync_Subdirectory_ResolvesRelativeForumLinks()
    {
        // Arrange
        string? requestUrl = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(request =>
        {
            requestUrl = request.RequestUri!.AbsoluteUri;
            return HtmlResponse(
                """
                <div class="node node--forum">
                    <div class="node-title"><a href="forums/movies.9/">Movies</a></div>
                    <div class="node-subNodesFlat"><a href="forums/series.10/">Series</a></div>
                </div>
                """
            );
        });
        using var client = CreateClient(handler, "https://example.org/community/");

        // Act
        var tree = await client.GetForumTreeAsync(CancellationToken.None);

        // Assert
        requestUrl.ShouldBe("https://example.org/community/");
        var forum = tree.ShouldHaveSingleItem();
        forum.Id.Value.ShouldBe("https://example.org/community/forums/movies.9/");
        forum
            .Children.ShouldHaveSingleItem()
            .Id.Value.ShouldBe("https://example.org/community/forums/series.10/");
    }

    [Test]
    public async Task SearchThreadsAsync_Subdirectory_KeepsSearchAndResultsInsideInstallation()
    {
        // Arrange
        var requestUrls = new List<string>();
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(request =>
        {
            requestUrls.Add(request.RequestUri!.AbsoluteUri);
            return HtmlResponse("<html data-csrf='token'></html>");
        });
        handler.Enqueue(request =>
        {
            requestUrls.Add(request.RequestUri!.AbsoluteUri);
            return HtmlResponse(
                """
                <div class="contentRow-title"><a href="threads/movie.42/">Movie</a></div>
                """
            );
        });
        using var client = CreateClient(handler, "https://example.org/community/");

        // Act
        var results = await client.SearchThreadsAsync(
            keywords: "Movie",
            forumUrl: "https://example.org/community/forums/9/",
            cancellationToken: CancellationToken.None
        );

        // Assert
        requestUrls.ShouldBe([
            "https://example.org/community/search/",
            "https://example.org/community/search/search",
        ]);
        results
            .ShouldHaveSingleItem()
            .Url.ShouldBe("https://example.org/community/threads/movie.42/");
    }

    [Test]
    public async Task SubmitNewThreadAsync_Subdirectory_ResolvesFormActionAgainstForumBaseUrl()
    {
        // Arrange
        var requestUrls = new List<string>();
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(request =>
        {
            requestUrls.Add(request.RequestUri!.AbsoluteUri);
            return HtmlResponse(
                """
                <html data-csrf="token">
                    <form action="forums/9/post-thread"><input type="hidden" name="_xfToken" value="token"></form>
                </html>
                """
            );
        });
        handler.Enqueue(request =>
        {
            requestUrls.Add(request.RequestUri!.AbsoluteUri);
            return JsonResponse("""{"status":"ok","redirect":"threads/movie.42/"}""");
        });
        using var client = CreateClient(handler, "https://example.org/community/");

        // Act
        var result = await client.SubmitNewThreadAsync(
            forumUrl: "https://example.org/community/forums/9/",
            title: "Movie",
            prefixIds: [],
            message: "Body",
            cancellationToken: CancellationToken.None
        );

        // Assert
        requestUrls.ShouldBe([
            "https://example.org/community/forums/9/post-thread",
            "https://example.org/community/forums/9/post-thread",
        ]);
        result.Url.ShouldBe("https://example.org/community/threads/movie.42/");
    }

    [Test]
    public async Task IsLoggedInAsync_SessionCookies_OnlySendsMatchingDomainAndPath()
    {
        // Arrange
        string? cookieHeader = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(request =>
        {
            cookieHeader = string.Join("; ", request.Headers.GetValues("Cookie"));
            return HtmlResponse("<html data-logged-in='true'></html>");
        });
        var session = new DistributionSession(
            BaseUrl: "https://example.org/community/",
            UserAgent: "Bearcat",
            Cookies:
            [
                new SessionCookie(
                    Name: "xf_user",
                    Value: "mine",
                    Domain: "example.org",
                    Path: "/community/"
                ),
                new SessionCookie(
                    Name: "other_path",
                    Value: "hidden",
                    Domain: "example.org",
                    Path: "/admin/"
                ),
                new SessionCookie(
                    Name: "other_domain",
                    Value: "hidden",
                    Domain: "other.example",
                    Path: "/"
                ),
            ]
        );
        using var client = CreateClient(
            handler: handler,
            baseUrl: "https://example.org/community/",
            session: session
        );

        // Act
        var loggedIn = await client.IsLoggedInAsync(CancellationToken.None);

        // Assert
        loggedIn.ShouldBeTrue();
        cookieHeader.ShouldBe("xf_user=mine");
    }

    [Test]
    public async Task SearchThreadsAsync_Redirect_UsesGetAndAcceptsUpdatedCookies()
    {
        // Arrange
        HttpMethod? searchMethod = null;
        HttpMethod? redirectMethod = null;
        HttpContent? redirectContent = null;
        string? redirectUrl = null;
        string? redirectCookies = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse("<html data-csrf='token'></html>"));
        handler.Enqueue(request =>
        {
            searchMethod = request.Method;
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("/community/search/42/", UriKind.Relative);
            response.Headers.Add("Set-Cookie", "xf_session=updated; Path=/community/");
            return response;
        });
        handler.Enqueue(request =>
        {
            redirectMethod = request.Method;
            redirectContent = request.Content;
            redirectUrl = request.RequestUri!.AbsoluteUri;
            redirectCookies = string.Join("; ", request.Headers.GetValues("Cookie"));
            return HtmlResponse("<html></html>");
        });
        using var client = CreateClient(handler, "https://example.org/community/");

        // Act
        await client.SearchThreadsAsync(
            keywords: "Movie",
            forumUrl: null,
            cancellationToken: CancellationToken.None
        );

        // Assert
        searchMethod.ShouldBe(HttpMethod.Post);
        redirectMethod.ShouldBe(HttpMethod.Get);
        redirectContent.ShouldBeNull();
        redirectUrl.ShouldBe("https://example.org/community/search/42/");
        redirectCookies.ShouldBe("xf_session=updated");
    }

    [TestCase(HttpStatusCode.TemporaryRedirect)]
    [TestCase(HttpStatusCode.PermanentRedirect)]
    public async Task SearchThreadsAsync_MethodPreservingRedirect_ResendsForm(HttpStatusCode status)
    {
        // Arrange
        HttpMethod? redirectMethod = null;
        string? redirectUrl = null;
        string? redirectBody = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse("<html data-csrf='token'></html>"));
        handler.Enqueue(_ => new HttpResponseMessage(status)
        {
            Headers = { Location = new Uri("/search/redirected", UriKind.Relative) },
        });
        handler.Enqueue(async request =>
        {
            redirectMethod = request.Method;
            redirectUrl = request.RequestUri!.AbsoluteUri;
            redirectBody = await request.Content!.ReadAsStringAsync();
            return HtmlResponse("<html></html>");
        });
        using var client = CreateClient(handler, "https://example.org/");

        // Act
        await client.SearchThreadsAsync(
            keywords: "Movie",
            forumUrl: null,
            cancellationToken: CancellationToken.None
        );

        // Assert
        redirectMethod.ShouldBe(HttpMethod.Post);
        redirectUrl.ShouldBe("https://example.org/search/redirected");
        redirectBody.ShouldNotBeNull();
        redirectBody.ShouldContain("keywords=Movie");
        redirectBody.ShouldContain("_xfToken=token");
    }

    [Test]
    public async Task SubmitReplyAsync_ForeignFormAction_DoesNotSendPostOrCookies()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ =>
            HtmlResponse(
                """
                <html data-csrf="token"><form action="https://other.example/add-reply"></form></html>
                """
            )
        );
        using var client = CreateClient(handler, "https://example.org/");

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            client.SubmitReplyAsync(
                threadUrl: "https://example.org/threads/42/",
                message: "Body",
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("different site");
    }

    [TestCase("https://other.example/")]
    [TestCase("http://example.org/")]
    public async Task IsLoggedInAsync_ForeignOrInsecureRedirect_DoesNotFollow(string redirectUrl)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri(redirectUrl) },
        });
        using var client = CreateClient(handler, "https://example.org/");

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            client.IsLoggedInAsync(CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain("different site");
    }

    [Test]
    public async Task LogInAsync_AcceptedCredentials_ReturnsSessionWithCookiesFromRedirect()
    {
        // Arrange
        string? loginBody = null;
        string? loginUrl = null;
        string? loginUserAgent = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(GuestHomePage));
        handler.Enqueue(_ => LoginPageResponse(LoginPage));
        handler.Enqueue(async request =>
        {
            loginUrl = request.RequestUri!.AbsoluteUri;
            loginUserAgent = request.Headers.UserAgent.ToString();
            loginBody = await request.Content!.ReadAsStringAsync();
            var response = new HttpResponseMessage(HttpStatusCode.SeeOther);
            response.Headers.Location = new Uri("https://www.data-load.me/");
            response.Headers.Add("Set-Cookie", "xf_user=42%2Cabc; path=/; httponly");
            response.Headers.Add("Set-Cookie", "xf_session=session-id; path=/; httponly");
            return response;
        });
        handler.Enqueue(_ => HtmlResponse("<html data-logged-in=\"true\"></html>"));
        using var client = CreateClient(handler);

        // Act
        var result = await client.LogInAsync(
            username: "uploader",
            password: "secret",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ErrorMessage.ShouldBeNull();
        result.Session.ShouldNotBeNull();
        result.Session.BaseUrl.ShouldBe("https://www.data-load.me/");
        result.Session.UserAgent.ShouldBe("Bearcat");
        result.Session.Cookies.ShouldContain(
            new SessionCookie(
                Name: "xf_user",
                Value: "42%2Cabc",
                Domain: "www.data-load.me",
                Path: "/"
            )
        );
        result.Session.Cookies.ShouldContain(
            new SessionCookie(
                Name: "xf_session",
                Value: "session-id",
                Domain: "www.data-load.me",
                Path: "/"
            )
        );
        loginUrl.ShouldBe("https://www.data-load.me/login/login");
        loginUserAgent.ShouldBe("Bearcat");
        loginBody.ShouldNotBeNull();
        loginBody.ShouldContain("login=uploader");
        loginBody.ShouldContain("password=secret");
        loginBody.ShouldContain("remember=1");
        loginBody.ShouldContain("_xfToken=form-token");
        loginBody.ShouldContain("_xfRedirect=https%3A%2F%2Fwww.data-load.me%2F");
    }

    [Test]
    public async Task LogInAsync_RejectedCredentials_ReturnsErrorTextOfForum()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(GuestHomePage));
        handler.Enqueue(_ => LoginPageResponse(LoginPage));
        handler.Enqueue(_ =>
            HtmlResponse(
                """
                <html data-logged-in="false">
                    <div class="blockMessage blockMessage--error blockMessage--iconic">
                        Incorrect password. <b>Please try again.</b>
                    </div>
                </html>
                """
            )
        );
        using var client = CreateClient(handler);

        // Act
        var result = await client.LogInAsync(
            username: "uploader",
            password: "wrong",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Session.ShouldBeNull();
        result.ErrorMessage.ShouldBe("Incorrect password. Please try again.");
    }

    [Test]
    public async Task LogInAsync_RedirectToTwoStepVerification_ReturnsTwoStepError()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(GuestHomePage));
        handler.Enqueue(_ => LoginPageResponse(LoginPage));
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.SeeOther)
        {
            Headers = { Location = new Uri("/login/two-step?_xfRedirect=%2F", UriKind.Relative) },
        });
        handler.Enqueue(_ => HtmlResponse("<html data-logged-in=\"false\"></html>"));
        using var client = CreateClient(handler);

        // Act
        var result = await client.LogInAsync(
            username: "uploader",
            password: "secret",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Session.ShouldBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("two-step");
    }

    [Test]
    public async Task LogInAsync_TwoStepFormOnRenamedRoute_ReturnsTwoStepError()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(GuestHomePage));
        handler.Enqueue(_ => LoginPageResponse(LoginPage));
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.SeeOther)
        {
            Headers = { Location = new Uri("/anmelden/zwei-schritte", UriKind.Relative) },
        });
        handler.Enqueue(_ =>
            HtmlResponse(
                """
                <html data-logged-in="false">
                    <form action="/anmelden/two-step" method="post">
                        <input type="text" name="code">
                    </form>
                </html>
                """
            )
        );
        using var client = CreateClient(handler);

        // Act
        var result = await client.LogInAsync(
            username: "uploader",
            password: "secret",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Session.ShouldBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("two-step");
    }

    [TestCase("<div data-xf-init=\"re-captcha\"></div>")]
    [TestCase("<div data-xf-init=\"qa-captcha\"></div>")]
    [TestCase("<div class=\"g-recaptcha\"></div>")]
    [TestCase("<div class=\"h-captcha\"></div>")]
    [TestCase("<div class=\"cf-turnstile\"></div>")]
    public async Task LogInAsync_CaptchaInLoginForm_ReturnsCaptchaErrorWithoutSendingCredentials(
        string captchaElement
    )
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(GuestHomePage));
        handler.Enqueue(_ =>
            LoginPageResponse(LoginPage.Replace("</form>", captchaElement + "</form>"))
        );
        using var client = CreateClient(handler);

        // Act
        var result = await client.LogInAsync(
            username: "uploader",
            password: "secret",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Session.ShouldBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("captcha");
        handler.SentRequestCount.ShouldBe(2);
    }

    [Test]
    public async Task LogInAsync_CloudflareChallengeOnHomePage_ReturnsChallengeErrorWithoutSendingCredentials()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    "<html><head><title>Just a moment...</title></head></html>",
                    Encoding.UTF8,
                    "text/html"
                ),
            };
            response.Headers.Add("cf-mitigated", "challenge");
            return response;
        });
        using var client = CreateClient(handler);

        // Act
        var result = await client.LogInAsync(
            username: "uploader",
            password: "secret",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Session.ShouldBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Cloudflare challenge");
        handler.SentRequestCount.ShouldBe(1);
    }

    [Test]
    public async Task LogInAsync_LoginPageWithPassiveCloudflareBeaconScript_SendsCredentials()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(GuestHomePage));
        handler.Enqueue(_ =>
            LoginPageResponse(
                LoginPage.Replace(
                    "</html>",
                    "<script src=\"/cdn-cgi/challenge-platform/scripts/jsd/main.js\"></script></html>"
                )
            )
        );
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.SeeOther)
        {
            Headers = { Location = new Uri("https://www.data-load.me/") },
        });
        handler.Enqueue(_ => HtmlResponse("<html data-logged-in=\"true\"></html>"));
        using var client = CreateClient(handler);

        // Act
        var result = await client.LogInAsync(
            username: "uploader",
            password: "secret",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ErrorMessage.ShouldBeNull();
        result.Session.ShouldNotBeNull();
        handler.SentRequestCount.ShouldBe(4);
    }

    [Test]
    public async Task LogInAsync_LoginLinkToRenamedRoute_UsesLinkedPageAndFormAction()
    {
        // Arrange
        var requestUrls = new List<string>();
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(request =>
        {
            requestUrls.Add(request.RequestUri!.AbsoluteUri);
            return HtmlResponse(GuestHomePage.Replace("/login/", "/anmelden/"));
        });
        handler.Enqueue(request =>
        {
            requestUrls.Add(request.RequestUri!.AbsoluteUri);
            return LoginPageResponse(LoginPage.Replace("/login/login", "/anmelden/login"));
        });
        handler.Enqueue(request =>
        {
            requestUrls.Add(request.RequestUri!.AbsoluteUri);
            return new HttpResponseMessage(HttpStatusCode.SeeOther)
            {
                Headers = { Location = new Uri("https://www.data-load.me/") },
            };
        });
        handler.Enqueue(_ => HtmlResponse("<html data-logged-in=\"true\"></html>"));
        using var client = CreateClient(handler);

        // Act
        var result = await client.LogInAsync(
            username: "uploader",
            password: "secret",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Session.ShouldNotBeNull();
        requestUrls.ShouldBe([
            "https://www.data-load.me/",
            "https://www.data-load.me/anmelden/",
            "https://www.data-load.me/anmelden/login",
        ]);
    }

    [Test]
    public async Task LogInAsync_LoginFormOnHomePage_SendsCredentialsWithoutOpeningLoginPage()
    {
        // Arrange
        string? loginUrl = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => LoginPageResponse(LoginPage));
        handler.Enqueue(request =>
        {
            loginUrl = request.RequestUri!.AbsoluteUri;
            return new HttpResponseMessage(HttpStatusCode.SeeOther)
            {
                Headers = { Location = new Uri("https://www.data-load.me/") },
            };
        });
        handler.Enqueue(_ => HtmlResponse("<html data-logged-in=\"true\"></html>"));
        using var client = CreateClient(handler);

        // Act
        var result = await client.LogInAsync(
            username: "uploader",
            password: "secret",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Session.ShouldNotBeNull();
        loginUrl.ShouldBe("https://www.data-load.me/login/login");
        handler.SentRequestCount.ShouldBe(3);
    }

    [Test]
    public async Task LogInAsync_HomePageWithoutLoginLink_OpensDefaultLoginRoute()
    {
        // Arrange
        string? loginPageUrl = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse("<html data-logged-in=\"false\"></html>"));
        handler.Enqueue(request =>
        {
            loginPageUrl = request.RequestUri!.AbsoluteUri;
            return LoginPageResponse(LoginPage.Replace("/login/login", "/community/login/login"));
        });
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.SeeOther)
        {
            Headers = { Location = new Uri("https://example.org/community/") },
        });
        handler.Enqueue(_ => HtmlResponse("<html data-logged-in=\"true\"></html>"));
        using var client = CreateClient(handler, "https://example.org/community/");

        // Act
        var result = await client.LogInAsync(
            username: "uploader",
            password: "secret",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Session.ShouldNotBeNull();
        loginPageUrl.ShouldBe("https://example.org/community/login/");
    }

    private const string GuestHomePage = """
        <html data-logged-in="false" data-csrf="page-token">
            <div class="p-navgroup p-navgroup--guest">
                <a href="/login/" class="p-navgroup-link p-navgroup-link--textual p-navgroup-link--logIn"
                    data-xf-click="overlay" rel="nofollow">Log in</a>
                <a href="/register/" class="p-navgroup-link p-navgroup-link--textual p-navgroup-link--register"
                    data-xf-click="overlay" rel="nofollow">Register</a>
            </div>
        </html>
        """;

    private const string LoginPage = """
        <html data-logged-in="false" data-csrf="page-token">
            <form action="/login/login" method="post" class="block">
                <input type="hidden" name="_xfToken" value="form-token">
                <input type="text" name="login">
                <input type="password" name="password">
                <input type="checkbox" name="remember" value="1" checked="checked">
                <input type="hidden" name="_xfRedirect" value="https://www.data-load.me/">
                <button type="submit">Log in</button>
            </form>
        </html>
        """;

    private const string EditPostPage = """
        <html data-csrf="page-token">
            <form action="/posts/99/edit" method="post">
                <input type="hidden" name="_xfToken" value="form-token">
                <input type="hidden" name="attachment_hash" value="hash-789">
                <input type="hidden" name="_xfResponseType" value="html">
                <input type="text" name="title" value="My Release">
                <input type="text" name="tags" value="action,thriller">
                <input type="checkbox" name="silent" value="1">
                <input type="submit" name="save" value="Save changes">
                <select name="prefix_id">
                    <option value="0">None</option>
                    <option value="7" selected>UHD</option>
                </select>
                <textarea name="message">Old body</textarea>
            </form>
        </html>
        """;

    private const string EditPostWithMultiSelectPage = """
        <html data-csrf="page-token">
            <form action="/posts/99/edit" method="post">
                <input type="hidden" name="_xfToken" value="form-token">
                <input type="checkbox" name="sticky" value="1" checked>
                <input type="checkbox" name="lock" value="1">
                <select name="prefix_id[]" multiple>
                    <option value="3" selected>Movie</option>
                    <option value="5">Series</option>
                    <option value="7" selected>UHD</option>
                </select>
                <textarea name="message">Old body</textarea>
            </form>
        </html>
        """;

    private const string LoggedInHomePage = """
        <html data-csrf="page-token">
            <a class="p-navgroup-link--user"><span class="p-navgroup-linkText">Bearcat</span></a>
        </html>
        """;

    private const string ThreadWithPostsPage = """
        <html data-csrf="page-token">
            <article class="message message--post" data-author="Someone">
                <div class="message-attribution-main"><a href="/threads/my-release.42/post-98"></a></div>
            </article>
            <article class="message message--post" data-author="Bearcat">
                <div class="message-attribution-main"><a href="/threads/my-release.42/post-99"></a></div>
            </article>
        </html>
        """;

    private const string NewThreadPage = """
        <html data-csrf="page-token">
            <form action="/forums/uhd-4k.9/post-thread" method="post">
                <input type="hidden" name="_xfToken" value="form-token">
                <input type="hidden" name="attachment_hash" value="hash-123">
                <input type="hidden" name="last_date" value="1700000000">
                <input type="hidden" name="kzOb4" value="">
                <input type="hidden" name="_xfResponseType" value="html">
                <input type="hidden" name="title" value="Stale Draft">
                <select name="prefix_id[]"><option value="3">Movie</option></select>
            </form>
        </html>
        """;

    private const string ThreadPage = """
        <html data-csrf="page-token">
            <form action="/threads/my-release.42/add-reply" method="post">
                <input type="hidden" name="_xfToken" value="form-token">
                <input type="hidden" name="attachment_hash" value="hash-456">
                <input type="hidden" name="message" value="Stale Draft">
            </form>
        </html>
        """;

    private static XenForoForumClient CreateClient(
        HttpMessageHandler handler,
        string baseUrl = "https://www.data-load.me/",
        DistributionSession? session = null
    )
    {
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactory
            .Setup(factory => factory.CreateClient(XenForoForumClient.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        return new XenForoForumClient(
            httpClientFactory: httpClientFactory.Object,
            baseUri: new Uri(baseUrl),
            session: session
                ?? new DistributionSession(BaseUrl: baseUrl, UserAgent: "Bearcat", Cookies: [])
        );
    }

    private static HttpResponseMessage HtmlResponse(string html)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
        };
    }

    private static HttpResponseMessage LoginPageResponse(string html)
    {
        var response = HtmlResponse(html);
        response.Headers.Add("Set-Cookie", "xf_csrf=csrf-cookie; path=/; secure");
        return response;
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> responses =
            new();

        public int SentRequestCount { get; private set; }

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            responses.Enqueue(request => Task.FromResult(responseFactory(request)));
        }

        public void Enqueue(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
        {
            responses.Enqueue(responseFactory);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            responses.Count.ShouldBeGreaterThan(0);
            SentRequestCount++;

            var response = await responses.Dequeue()(request);
            response.RequestMessage ??= request;
            return response;
        }
    }
}
