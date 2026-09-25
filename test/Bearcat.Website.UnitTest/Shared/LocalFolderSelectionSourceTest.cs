using Bearcat.Abstractions;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bearcat.Website.UnitTest.Shared;

public class LocalFolderSelectionSourceTest
{
    private static readonly string Root = Path.GetPathRoot(Path.GetTempPath())!;
    private static readonly string Releases = Path.Combine(Root, "releases");
    private static readonly string Movies = Path.Combine(Releases, "movies");

    private readonly LocalFolderSelectionSource source = new(null!, [Releases]);

    [Test]
    public void RootPaths_ReturnsConfiguredPaths()
    {
        // Act
        var rootPaths = source.RootPaths;

        // Assert
        rootPaths.ShouldBe([Releases]);
    }

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

    [Test]
    public void NormalizePath_RelativeSegments_AreResolved()
    {
        // Act
        var result = source.NormalizePath(Path.Combine(Movies, ".."));

        // Assert
        result.ShouldBe(Releases);
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

    [Test]
    public void GetDisplayName_Folder_ReturnsFolderName()
    {
        // Act
        var result = source.GetDisplayName(Movies);

        // Assert
        result.ShouldBe("movies");
    }

    [Test]
    public void GetDisplayName_Root_ReturnsPath()
    {
        // Act
        var result = source.GetDisplayName(Root);

        // Assert
        result.ShouldBe(Root);
    }

    [Test]
    public async Task GetChildEntriesAsync_FilesNotIncluded_ReturnsOnlyFolders()
    {
        // Arrange
        var tempRootPath = CreateTempFolderWithSubfolderAndFile();
        await using var serviceProvider = CreateServiceProvider();
        var folderSource = new LocalFolderSelectionSource(
            serviceProvider.GetRequiredService<IScopedOperationRunner>(),
            [tempRootPath]
        );

        try
        {
            // Act
            var result = await folderSource.GetChildEntriesAsync(tempRootPath);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.FolderPaths.ShouldBe([Path.Combine(tempRootPath, "ads")]);
            result.FilePaths.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task GetChildEntriesAsync_FilesIncluded_ReturnsFoldersAndFiles()
    {
        // Arrange
        var tempRootPath = CreateTempFolderWithSubfolderAndFile();
        await using var serviceProvider = CreateServiceProvider();
        var folderAndFileSource = new LocalFolderSelectionSource(
            serviceProvider.GetRequiredService<IScopedOperationRunner>(),
            [tempRootPath],
            includeFiles: true
        );

        try
        {
            // Act
            var result = await folderAndFileSource.GetChildEntriesAsync(tempRootPath);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.FolderPaths.ShouldBe([Path.Combine(tempRootPath, "ads")]);
            result.FilePaths.ShouldBe([Path.Combine(tempRootPath, "premium.txt")]);
        }
        finally
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    private static string CreateTempFolderWithSubfolderAndFile()
    {
        var tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRootPath, "ads"));
        File.WriteAllText(Path.Combine(tempRootPath, "premium.txt"), "premium");

        return tempRootPath;
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IScopedOperationRunner, ScopedOperationRunner>();

        return services.BuildServiceProvider();
    }
}
