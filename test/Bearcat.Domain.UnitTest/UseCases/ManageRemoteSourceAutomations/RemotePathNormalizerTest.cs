using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageRemoteSourceAutomations;

public class RemotePathNormalizerTest
{
    [TestCase("/", "/")]
    [TestCase("///", "/")]
    [TestCase(" /incoming ", "/incoming")]
    [TestCase("incoming", "/incoming")]
    [TestCase("/incoming/", "/incoming")]
    [TestCase("incoming/tv//", "/incoming/tv")]
    [TestCase("/incoming/tv", "/incoming/tv")]
    public void Normalize_ValidPath_ReturnsRootedPathWithoutTrailingSlash(
        string remotePath,
        string expected
    )
    {
        // Act
        var normalized = RemotePathNormalizer.Normalize(remotePath);

        // Assert
        normalized.ShouldBe(expected);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Normalize_EmptyPath_Throws(string remotePath)
    {
        // Act
        var action = () => RemotePathNormalizer.Normalize(remotePath);

        // Assert
        action.ShouldThrow<ArgumentException>();
    }
}
