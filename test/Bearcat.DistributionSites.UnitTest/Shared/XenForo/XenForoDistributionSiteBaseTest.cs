using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.DistributionSites.Shared.XenForo;
using Moq;
using Shouldly;

namespace Bearcat.DistributionSites.UnitTest.Shared.XenForo;

public class XenForoDistributionSiteBaseTest
{
    [Test]
    public void CreateForumTargetIdFromStoredValue_NumericNodeId_BuildsForumUrl()
    {
        // Arrange
        var site = new TestForumSite(Mock.Of<IHttpClientFactory>());
        var session = CreateSession("https://www.data-load.me/");

        // Act
        var target = site.CreateForumTargetIdFromStoredValue(session, "123");

        // Assert
        target.Value.ShouldBe("https://www.data-load.me/forums/123/");
    }

    [Test]
    public void CreateForumTargetIdFromStoredValue_BaseUrlWithSubdirectory_BuildsForumUrlInSubdirectory()
    {
        // Arrange
        var site = new TestForumSite(Mock.Of<IHttpClientFactory>());
        var session = CreateSession("https://example.org/community");

        // Act
        var target = site.CreateForumTargetIdFromStoredValue(session, " 42 ");

        // Assert
        target.Value.ShouldBe("https://example.org/community/forums/42/");
    }

    [Test]
    public void CreateForumTargetIdFromStoredValue_AbsoluteUrl_PassesThrough()
    {
        // Arrange
        var site = new TestForumSite(Mock.Of<IHttpClientFactory>());
        var session = CreateSession("https://www.data-load.me/");

        // Act
        var target = site.CreateForumTargetIdFromStoredValue(
            session,
            "https://www.data-load.me/forums/uhd-4k.9/"
        );

        // Assert
        target.Value.ShouldBe("https://www.data-load.me/forums/uhd-4k.9/");
    }

    private static DistributionSession CreateSession(string baseUrl)
    {
        return new DistributionSession(BaseUrl: baseUrl, UserAgent: "Bearcat", Cookies: []);
    }

    private sealed class TestForumSite(IHttpClientFactory httpClientFactory)
        : XenForoDistributionSiteBase<TestForumSiteConfig>(httpClientFactory)
    {
        public override string Name => "Test Forum";

        public override string GetBaseUrl(IDistributionSiteConfig config) =>
            "https://www.data-load.me/";
    }

    private sealed record TestForumSiteConfig : IXenForoDistributionSiteConfig
    {
        public string Username => string.Empty;

        public string Password => string.Empty;

        public IReadOnlyDictionary<string, object?> ToDictionary() =>
            new Dictionary<string, object?>();
    }
}
