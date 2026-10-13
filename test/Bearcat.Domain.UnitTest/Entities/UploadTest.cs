using Bearcat.Domain.Entities;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Entities;

public class UploadTest
{
    [Test]
    public void GetHosterClassName_HosterRegistrationAssigned_ReturnsHosterClassNameOfHosterRegistration()
    {
        // Arrange
        var hosterAccountPool = CreateHosterAccountPool("PoolHoster");
        var hosterRegistration = CreateHosterRegistration("RegistrationHoster");
        var upload = new Upload
        {
            UploadConfig = new UploadConfig
            {
                HosterAccountPoolId = hosterAccountPool.Id,
                HosterAccountPool = hosterAccountPool,
            },
            HosterRegistrationId = hosterRegistration.Id,
            HosterRegistration = hosterRegistration,
        };

        // Act
        var result = upload.GetHosterClassName();

        // Assert
        result.ShouldBe("RegistrationHoster");
    }

    [Test]
    public void GetHosterClassName_NoHosterRegistrationAssigned_ReturnsHosterClassNameOfHosterAccountPool()
    {
        // Arrange
        var hosterAccountPool = CreateHosterAccountPool("PoolHoster");
        var upload = new Upload
        {
            UploadConfig = new UploadConfig
            {
                HosterAccountPoolId = hosterAccountPool.Id,
                HosterAccountPool = hosterAccountPool,
            },
        };

        // Act
        var result = upload.GetHosterClassName();

        // Assert
        result.ShouldBe("PoolHoster");
    }

    [Test]
    public void GetHosterClassName_UploadConfigTargetsHosterRegistration_ReturnsHosterClassNameOfUploadConfig()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration("RegistrationHoster");
        var uploadConfig = new UploadConfig
        {
            HosterRegistrationId = hosterRegistration.Id,
            HosterRegistration = hosterRegistration,
        };

        // Act
        var result = uploadConfig.GetHosterClassName();

        // Assert
        result.ShouldBe("RegistrationHoster");
    }

    [Test]
    public void GetUploadTargetName_UploadConfigTargetsHosterAccountPool_ReturnsNameOfHosterAccountPool()
    {
        // Arrange
        var hosterAccountPool = CreateHosterAccountPool("PoolHoster");
        var uploadConfig = new UploadConfig
        {
            HosterAccountPoolId = hosterAccountPool.Id,
            HosterAccountPool = hosterAccountPool,
        };

        // Act
        var result = uploadConfig.GetUploadTargetName();

        // Assert
        result.ShouldBe("Main pool");
    }

    private static HosterAccountPool CreateHosterAccountPool(string hosterClassName) =>
        new()
        {
            Id = 7,
            Name = "Main pool",
            HosterClassName = hosterClassName,
        };

    private static HosterRegistration CreateHosterRegistration(string hosterClassName) =>
        new()
        {
            Id = 3,
            Name = "Account",
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
        };
}
