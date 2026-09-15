using Bearcat.DistributionSites.Shared.XenForo;
using Moq;
using Shouldly;

namespace Bearcat.DistributionSites.UnitTest.Shared.XenForo;

public class XenForoDistributionSiteBaseTest
{
    [Test]
    public void ResolveTarget_NumericNodeId_BuildsForumUrl()
    {
        // Arrange
        var site = new TestForumSite(Mock.Of<IHttpClientFactory>());

        // Act
        var target = site.ResolveTarget("123");

        // Assert
        target.Value.ShouldBe("https://www.data-load.me/forums/123/");
    }

    [Test]
    public void ResolveTarget_AbsoluteUrl_PassesThrough()
    {
        // Arrange
        var site = new TestForumSite(Mock.Of<IHttpClientFactory>());

        // Act
        var target = site.ResolveTarget("https://www.data-load.me/forums/uhd-4k.9/");

        // Assert
        target.Value.ShouldBe("https://www.data-load.me/forums/uhd-4k.9/");
    }

    private sealed class TestForumSite(IHttpClientFactory httpClientFactory)
        : XenForoDistributionSiteBase<TestForumSiteConfig>(httpClientFactory)
    {
        public override string Name => "Test Forum";

        public override string BaseUrl => "https://www.data-load.me/";
    }

    private sealed record TestForumSiteConfig : IXenForoDistributionSiteConfig
    {
        public string Username => string.Empty;

        public string Password => string.Empty;

        public IReadOnlyDictionary<string, string> ToDictionary() =>
            new Dictionary<string, string>();
    }
}
