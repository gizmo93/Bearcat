using Bearcat.Website.Shared;
using Shouldly;

namespace Bearcat.Website.UnitTest.Shared;

public class CopyNameGeneratorTest
{
    private const string CopyNameFormat = "{0} (copy)";
    private const string NumberedCopyNameFormat = "{0} (copy {1})";

    [Test]
    public void CreateCopyName_NoCopyExists_ReturnsFirstCopyName()
    {
        // Arrange
        string[] existingNames = ["Movies"];

        // Act
        var copyName = CopyNameGenerator.CreateCopyName(
            "Movies",
            existingNames,
            CopyNameFormat,
            NumberedCopyNameFormat
        );

        // Assert
        copyName.ShouldBe("Movies (copy)");
    }

    [Test]
    public void CreateCopyName_FirstCopyExists_ReturnsSecondCopyName()
    {
        // Arrange
        string[] existingNames = ["Movies", "Movies (copy)"];

        // Act
        var copyName = CopyNameGenerator.CreateCopyName(
            "Movies",
            existingNames,
            CopyNameFormat,
            NumberedCopyNameFormat
        );

        // Assert
        copyName.ShouldBe("Movies (copy 2)");
    }

    [Test]
    public void CreateCopyName_SeveralNumberedCopiesExist_ReturnsNextFreeNumber()
    {
        // Arrange
        string[] existingNames = ["Movies", "Movies (copy)", "Movies (copy 2)", "Movies (copy 3)"];

        // Act
        var copyName = CopyNameGenerator.CreateCopyName(
            "Movies",
            existingNames,
            CopyNameFormat,
            NumberedCopyNameFormat
        );

        // Assert
        copyName.ShouldBe("Movies (copy 4)");
    }

    [Test]
    public void CreateCopyName_CopyNameDiffersOnlyInCase_ReturnsFirstCopyName()
    {
        // Arrange
        string[] existingNames = ["Movies", "movies (copy)"];

        // Act
        var copyName = CopyNameGenerator.CreateCopyName(
            "Movies",
            existingNames,
            CopyNameFormat,
            NumberedCopyNameFormat
        );

        // Assert
        copyName.ShouldBe("Movies (copy)");
    }
}
