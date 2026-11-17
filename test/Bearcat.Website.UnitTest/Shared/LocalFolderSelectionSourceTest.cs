using Bearcat.Website.Shared;
using Shouldly;

namespace Bearcat.Website.UnitTest.Shared;

public class LocalFolderSelectionSourceTest
{
    private static readonly string Root = Path.GetPathRoot(Path.GetTempPath())!;
    private static readonly string Releases = Path.Combine(Root, "releases");
    private static readonly string Movies = Path.Combine(Releases, "movies");

    private readonly LocalFolderSelectionSource source = new(null!, [Releases]);

    [Test]
    public void NormalizePath_TrailingSeparator_IsRemoved()
    {
        // Act
        var result = source.NormalizePath(Movies + Path.DirectorySeparatorChar);

        // Assert
        result.ShouldBe(Movies);
    }

    [Test]
    public void NormalizePath_Root_KeepsTrailingSeparator()
    {
        // Act
        var result = source.NormalizePath(Root);

        // Assert
        result.ShouldBe(Root);
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

    [Test]
    public void GetParentPath_NestedPath_ReturnsParent()
    {
        // Act
        var result = source.GetParentPath(Movies + Path.DirectorySeparatorChar);

        // Assert
        result.ShouldBe(Releases);
    }

    [Test]
    public void GetParentPath_TopLevelFolder_ReturnsRoot()
    {
        // Act
        var result = source.GetParentPath(Releases);

        // Assert
        result.ShouldBe(Root);
    }

    [Test]
    public void GetParentPath_Root_ReturnsNull()
    {
        // Act
        var result = source.GetParentPath(Root);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public void IsSameOrDescendantPath_Descendant_ReturnsTrue()
    {
        // Act
        var result = source.IsSameOrDescendantPath(Movies, Releases);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsSameOrDescendantPath_SamePathWithTrailingSeparator_ReturnsTrue()
    {
        // Act
        var result = source.IsSameOrDescendantPath(
            Releases + Path.DirectorySeparatorChar,
            Releases
        );

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsSameOrDescendantPath_DescendantOfRoot_ReturnsTrue()
    {
        // Act
        var result = source.IsSameOrDescendantPath(Movies, Root);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsSameOrDescendantPath_SiblingWithSamePrefix_ReturnsFalse()
    {
        // Act
        var result = source.IsSameOrDescendantPath(Releases + "-old", Releases);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsSameOrDescendantPath_Ancestor_ReturnsFalse()
    {
        // Act
        var result = source.IsSameOrDescendantPath(Releases, Movies);

        // Assert
        result.ShouldBeFalse();
    }
}
