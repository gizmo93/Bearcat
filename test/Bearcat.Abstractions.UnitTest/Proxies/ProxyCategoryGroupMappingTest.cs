using Bearcat.Abstractions.Proxies;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Proxies;

public class ProxyCategoryGroupMappingTest
{
    [Test]
    public void GetGroup_EveryCategory_IsMapped()
    {
        // Act
        var groups = Enum.GetValues<ProxyCategory>().Select(ProxyCategoryGroupMapping.GetGroup);

        // Assert
        groups.Count().ShouldBe(Enum.GetValues<ProxyCategory>().Length);
    }
}
