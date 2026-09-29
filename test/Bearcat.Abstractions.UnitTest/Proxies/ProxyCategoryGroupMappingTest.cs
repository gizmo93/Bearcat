using Bearcat.Abstractions.Proxies;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Proxies;

public class ProxyCategoryGroupMappingTest
{
    [TestCase(ProxyCategory.HosterUploads, ProxyCategoryGroup.Hosters)]
    [TestCase(ProxyCategory.HosterMirrorDownloads, ProxyCategoryGroup.Hosters)]
    [TestCase(ProxyCategory.ImageHosters, ProxyCategoryGroup.ImageHosters)]
    [TestCase(ProxyCategory.LinkCrypters, ProxyCategoryGroup.LinkCrypters)]
    [TestCase(ProxyCategory.NfoDatabases, ProxyCategoryGroup.NfoDatabases)]
    [TestCase(ProxyCategory.MediaDatabases, ProxyCategoryGroup.MediaDatabases)]
    public void GetGroup_Category_ReturnsPluginGroup(
        ProxyCategory category,
        ProxyCategoryGroup expectedGroup
    )
    {
        // Act
        var group = ProxyCategoryGroupMapping.GetGroup(category);

        // Assert
        group.ShouldBe(expectedGroup);
    }

    [Test]
    public void GetGroup_EveryCategory_IsMapped()
    {
        // Act
        var groups = Enum.GetValues<ProxyCategory>().Select(ProxyCategoryGroupMapping.GetGroup);

        // Assert
        groups.Count().ShouldBe(Enum.GetValues<ProxyCategory>().Length);
    }
}
