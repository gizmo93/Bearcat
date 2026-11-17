using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageProxyServers.CategoryDefaults;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageProxyServers;

public class ProxyCategoryDefaultServiceTest
{
    private List<ProxyCategoryDefault> storedDefaults = null!;
    private Mock<IProxyCategoryDefaultWriteRepository> writeRepositoryMock = null!;
    private Mock<IProxyRoutingCache> proxyRoutingCacheMock = null!;
    private ProxyCategoryDefaultService service = null!;

    [SetUp]
    public void SetUp()
    {
        storedDefaults = [];
        writeRepositoryMock = new Mock<IProxyCategoryDefaultWriteRepository>();
        writeRepositoryMock
            .Setup(repository => repository.GetAllForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedDefaults.ToList());
        writeRepositoryMock
            .Setup(repository => repository.Add(It.IsAny<ProxyCategoryDefault>()))
            .Callback(
                (ProxyCategoryDefault proxyCategoryDefault) =>
                    storedDefaults.Add(proxyCategoryDefault)
            );
        proxyRoutingCacheMock = new Mock<IProxyRoutingCache>();

        service = new ProxyCategoryDefaultService(
            writeRepositoryMock.Object,
            proxyRoutingCacheMock.Object
        );
    }

    [Test]
    public async Task SetDefaultForAllCategoriesAsync_SomeStoredDefaults_SetsEveryCategory()
    {
        // Arrange
        storedDefaults.Add(
            new ProxyCategoryDefault
            {
                ProxyCategory = ProxyCategory.HosterUploads,
                ProxyServerId = null,
            }
        );

        // Act
        await service.SetDefaultForAllCategoriesAsync(7);

        // Assert
        storedDefaults
            .Select(proxyCategoryDefault => proxyCategoryDefault.ProxyCategory)
            .ShouldBe(Enum.GetValues<ProxyCategory>(), ignoreOrder: true);
        storedDefaults.ShouldAllBe(proxyCategoryDefault => proxyCategoryDefault.ProxyServerId == 7);
        VerifySavedAndCacheRefreshed();
    }

    private void VerifySavedAndCacheRefreshed()
    {
        writeRepositoryMock.Verify(
            repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
        proxyRoutingCacheMock.Verify(
            cache => cache.RefreshAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
