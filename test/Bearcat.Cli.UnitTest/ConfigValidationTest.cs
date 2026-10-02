using Shouldly;

namespace Bearcat.Cli.UnitTest;

public class ConfigValidationTest
{
    private string temporaryDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        temporaryDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void TestSqliteFileLocation_MissingDirectory_CreatesDirectoryAndSucceeds()
    {
        // Arrange
        var sqliteFilePath = Path.Combine(temporaryDirectory, "nested", "bearcat.db");

        // Act
        var (success, error) = ConfigValidation.TestSqliteFileLocation(sqliteFilePath);

        // Assert
        success.ShouldBeTrue();
        error.ShouldBeNull();
        Directory.Exists(Path.Combine(temporaryDirectory, "nested")).ShouldBeTrue();
        Directory.GetFiles(Path.Combine(temporaryDirectory, "nested")).ShouldBeEmpty();
    }

    [Test]
    public void TestSqliteFileLocation_RelativePath_Fails()
    {
        // Act
        var (success, error) = ConfigValidation.TestSqliteFileLocation("bearcat.db");

        // Assert
        success.ShouldBeFalse();
        error.ShouldBe("The SQLite database file must be an absolute path.");
    }

    [Test]
    public void TestSqliteFileLocation_EmptyPath_Fails()
    {
        // Act
        var (success, error) = ConfigValidation.TestSqliteFileLocation(" ");

        // Assert
        success.ShouldBeFalse();
        error.ShouldBe("The SQLite database file is required.");
    }

    [Test]
    public void TestSqliteFileLocation_ExistingDirectory_Fails()
    {
        // Arrange
        Directory.CreateDirectory(temporaryDirectory);

        // Act
        var (success, error) = ConfigValidation.TestSqliteFileLocation(temporaryDirectory);

        // Assert
        success.ShouldBeFalse();
        error.ShouldBe("The SQLite database file must be a file, not a directory.");
    }
}
