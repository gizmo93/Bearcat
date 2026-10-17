using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.UseCases.ManageUploadConfigs.UploadSources;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Moq;

namespace Bearcat.Domain.IntegrationTest.Shared.UploadSources;

public static class TestUploadSourceValidator
{
    public const string VideoHosterClassName = "TestVideoHoster";

    public static UploadSourceValidator Create(BearcatDbContext dbContext)
    {
        var hosterFactoryMock = new Mock<IHosterFactory>();
        hosterFactoryMock
            .Setup(factory => factory.GetByName(It.IsAny<string>()))
            .Returns(Mock.Of<IHoster>());
        hosterFactoryMock
            .Setup(factory => factory.GetByName(VideoHosterClassName))
            .Returns(Mock.Of<IVideoHoster>());

        return new UploadSourceValidator(
            new UploadConfigReadRepository(dbContext),
            hosterFactoryMock.Object
        );
    }
}
