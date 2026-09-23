using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;

public class RemoteFolderNameValidatorTest
{
    [TestCase("Bearcat.Release.1080p.WEB.x264-GRP")]
    [TestCase("Release (2026) [Extended]")]
    [TestCase(".hidden.release")]
    [TestCase("Release..Name")]
    public void IsSafe_RegularFolderName_ReturnsTrue(string folderName)
    {
        // Act
        var isSafe = RemoteFolderNameValidator.IsSafe(folderName);

        // Assert
        isSafe.ShouldBeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(".")]
    [TestCase("..")]
    [TestCase("...")]
    [TestCase("../escape")]
    [TestCase("..\\escape")]
    [TestCase("nested/release")]
    [TestCase("nested\\release")]
    [TestCase("/rooted")]
    [TestCase("\\rooted")]
    [TestCase("null\0byte")]
    public void IsSafe_UnsafeFolderName_ReturnsFalse(string folderName)
    {
        // Act
        var isSafe = RemoteFolderNameValidator.IsSafe(folderName);

        // Assert
        isSafe.ShouldBeFalse();
    }
}
