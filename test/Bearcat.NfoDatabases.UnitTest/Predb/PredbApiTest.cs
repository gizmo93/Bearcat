using System.Net;
using Bearcat.NfoDatabases.Predb.Api;
using Moq;
using Moq.Protected;
using Refit;
using Shouldly;

namespace Bearcat.NfoDatabases.UnitTest.Predb;

public class PredbApiTest
{
    private Uri? requestedUri;
    private IPredbApi api = null!;

    [SetUp]
    public void SetUp()
    {
        requestedUri = null;

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(
                (HttpRequestMessage request, CancellationToken _) =>
                {
                    requestedUri = request.RequestUri;
                    return Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                """{"status":"success","message":"","results":0}""",
                                System.Text.Encoding.UTF8,
                                "application/json"
                            ),
                        }
                    );
                }
            );

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.predb.net/"),
        };
        api = RestService.For<IPredbApi>(httpClient);
    }

    [Test]
    public async Task GetPreAsync_BuildsTypeAndReleaseQuery()
    {
        // Act
        await api.GetPreAsync("Movie.Release.2026-GRP", CancellationToken.None);

        // Assert
        requestedUri.ShouldNotBeNull();
        requestedUri.GetLeftPart(UriPartial.Path).ShouldBe("https://api.predb.net/");
        ParseQuery(requestedUri)["type"].ShouldBe("pre");
        ParseQuery(requestedUri)["release"].ShouldBe("Movie.Release.2026-GRP");
    }

    [Test]
    public async Task GetNfoAsync_BuildsTypeAndReleaseQuery()
    {
        // Act
        await api.GetNfoAsync("Movie Release & Friends-GRP", CancellationToken.None);

        // Assert
        requestedUri.ShouldNotBeNull();
        ParseQuery(requestedUri)["type"].ShouldBe("nfo");
        ParseQuery(requestedUri)["release"].ShouldBe("Movie Release & Friends-GRP");
    }

    private static Dictionary<string, string> ParseQuery(Uri uri)
    {
        return uri
            .Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));
    }
}
