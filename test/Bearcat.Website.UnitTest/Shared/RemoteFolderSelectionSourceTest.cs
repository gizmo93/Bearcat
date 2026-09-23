using Bearcat.Website.Shared;
using Shouldly;

namespace Bearcat.Website.UnitTest.Shared;

public class RemoteFolderSelectionSourceTest
{
    private readonly RemoteFolderSelectionSource source = new(null!, 1);

    [Test]
    public void RootPaths_ContainsOnlyTheRemoteRoot()
    {
        // Act
        var rootPaths = source.RootPaths;

        // Assert
        rootPaths.ShouldBe(["/"]);
    }

    [TestCase("/", "/")]
    [TestCase("//", "/")]
    [TestCase("releases", "/releases")]
    [TestCase("/releases/", "/releases")]
    [TestCase(" /releases/movies/ ", "/releases/movies")]
    public void NormalizePath_Path_ReturnsRootedPathWithoutTrailingSeparator(
        string path,
        string expected
    )
    {
        // Act
        var result = source.NormalizePath(path);

        // Assert
        result.ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void NormalizePath_BlankPath_ReturnsNull(string? path)
    {
        // Act
        var result = source.NormalizePath(path);

        // Assert
        result.ShouldBeNull();
    }

    [TestCase("/releases", "/")]
    [TestCase("/releases/movies", "/releases")]
    [TestCase("/releases/movies/", "/releases")]
    public void GetParentPath_NestedPath_ReturnsParent(string path, string expected)
    {
        // Act
        var result = source.GetParentPath(path);

        // Assert
        result.ShouldBe(expected);
    }

    [Test]
    public void GetParentPath_Root_ReturnsNull()
    {
        // Act
        var result = source.GetParentPath("/");

        // Assert
        result.ShouldBeNull();
    }

    [TestCase("/releases", "/releases")]
    [TestCase("/releases/movies", "/releases")]
    [TestCase("/releases/movies", "/")]
    [TestCase("/", "/")]
    [TestCase("/releases/", "/releases")]
    public void IsSameOrDescendantPath_DescendantOrSame_ReturnsTrue(
        string path,
        string ancestorPath
    )
    {
        // Act
        var result = source.IsSameOrDescendantPath(path, ancestorPath);

        // Assert
        result.ShouldBeTrue();
    }

    [TestCase("/releases-old", "/releases")]
    [TestCase("/releases", "/releases/movies")]
    [TestCase("/", "/releases")]
    public void IsSameOrDescendantPath_Unrelated_ReturnsFalse(string path, string ancestorPath)
    {
        // Act
        var result = source.IsSameOrDescendantPath(path, ancestorPath);

        // Assert
        result.ShouldBeFalse();
    }

    [TestCase("/", "/")]
    [TestCase("/releases", "releases")]
    [TestCase("/releases/movies/", "movies")]
    public void GetDisplayName_Path_ReturnsLastSegment(string path, string expected)
    {
        // Act
        var result = source.GetDisplayName(path);

        // Assert
        result.ShouldBe(expected);
    }
}
