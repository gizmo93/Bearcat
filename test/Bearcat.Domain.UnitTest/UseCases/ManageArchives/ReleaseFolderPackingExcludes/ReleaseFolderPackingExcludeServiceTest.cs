using System.Linq.Expressions;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderPackingExcludes;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageArchives.ReleaseFolderPackingExcludes;

public class ReleaseFolderPackingExcludeServiceTest
{
    private string releaseFolderPath = null!;

    [SetUp]
    public void Setup()
    {
        releaseFolderPath = Directory
            .CreateDirectory(
                Path.Combine(Path.GetTempPath(), $"bearcat-excludes-{Guid.NewGuid():N}")
            )
            .FullName;

        foreach (
            var relativeFilePath in new List<string>
            {
                "movie.mkv",
                "a.sfv",
                "sample.txt",
                "Sub/b.SFV",
                "Sub/Sample/s.mkv",
                "Sample/x.mkv",
                "@eaDir/thumb.jpg",
            }
        )
        {
            var filePath = Path.Combine(releaseFolderPath, relativeFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "x");
        }
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(releaseFolderPath, recursive: true);
    }

    [TestCase("*.sfv", new[] { "a.sfv", "Sub/b.SFV" }, 2)]
    [TestCase("sample*/", new[] { "Sample", "Sub/Sample" }, 2)]
    [TestCase("sample*", new[] { "sample.txt" }, 1)]
    [TestCase("*.mkv,sample*/", new[] { "movie.mkv", "Sample", "Sub/Sample" }, 3)]
    [TestCase(" *.SFV , , @eaDir/ ", new[] { "a.sfv", "Sub/b.SFV", "@eaDir" }, 3)]
    [TestCase("", new string[0], 0)]
    public void FindExcludedEntries_PatternsConfigured_ReturnsMatchingEntriesWithoutDescendingIntoExcludedFolders(
        string patterns,
        string[] expectedRelativePaths,
        int expectedFileCount
    )
    {
        // Arrange
        var service = new ReleaseFolderPackingExcludeService(
            CreateFileSystemServiceMockDelegatingToRealFileSystem().Object,
            CreateConfigurationProvider(patterns)
        );

        // Act
        var result = service.FindExcludedEntries(releaseFolderPath);

        // Assert
        result.RelativePaths.ShouldBe(
            expectedRelativePaths
                .Select(path => path.Replace('/', Path.DirectorySeparatorChar))
                .ToList(),
            ignoreOrder: true
        );

        result.FileCount.ShouldBe(expectedFileCount);
        result.TotalBytes.ShouldBe(expectedFileCount);
    }

    private static IApplicationConfigurationProvider CreateConfigurationProvider(string patterns)
    {
        var configurationProviderMock = new Mock<IApplicationConfigurationProvider>();

        configurationProviderMock
            .Setup(provider =>
                provider.GetValue<ArchiveCreationConfiguration>(
                    It.IsAny<Expression<Func<ArchiveCreationConfiguration, string?>>>()
                )
            )
            .Returns(patterns);

        return configurationProviderMock.Object;
    }

    private static Mock<IFileSystemService> CreateFileSystemServiceMockDelegatingToRealFileSystem()
    {
        var fileSystemServiceMock = new Mock<IFileSystemService>();

        fileSystemServiceMock
            .Setup(service => service.GetFilesInPath(It.IsAny<string>(), false))
            .Returns((string path, bool _) => Directory.GetFiles(path).ToList());
        fileSystemServiceMock
            .Setup(service => service.GetFoldersInPath(It.IsAny<string>()))
            .Returns((string path) => Directory.GetDirectories(path).ToList());
        fileSystemServiceMock
            .Setup(service => service.DirectoryExists(It.IsAny<string>()))
            .Returns((string path) => Directory.Exists(path));
        fileSystemServiceMock
            .Setup(service => service.GetFileSizeBytes(It.IsAny<string>()))
            .Returns((string filePath) => new FileInfo(filePath).Length);
        fileSystemServiceMock
            .Setup(service => service.GetFolderFileCountAndSize(It.IsAny<string>()))
            .Returns(
                (string path) =>
                {
                    var files = new DirectoryInfo(path).GetFiles("*", SearchOption.AllDirectories);

                    return new FolderFileCountAndSize(files.Length, files.Sum(file => file.Length));
                }
            );

        return fileSystemServiceMock;
    }
}
