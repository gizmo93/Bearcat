using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.Shared.QualityGate.Checks;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.QualityGate;

public class QualityChecksTest
{
    [Test]
    public void FilePattern_MatchingFilePresent_ReturnsNoIssues()
    {
        // Arrange
        var check = new FilePatternQualityCheck();
        var rule = CreateRule(new Dictionary<string, object?> { ["pattern"] = "*.nfo" });
        var context = CreateContext(
            new FakeFileSystemService { Files = ["/release/movie.mkv", "/release/release.nfo"] }
        );

        // Act
        var issues = check.Evaluate(rule, context);

        // Assert
        issues.ShouldBeEmpty();
    }

    [Test]
    public void FilePattern_NoMatchingFile_ReturnsIssue()
    {
        // Arrange
        var check = new FilePatternQualityCheck();
        var rule = CreateRule(new Dictionary<string, object?> { ["pattern"] = "*.nfo" });
        var context = CreateContext(new FakeFileSystemService { Files = ["/release/movie.mkv"] });

        // Act
        var issues = check.Evaluate(rule, context);

        // Assert
        issues.ShouldHaveSingleItem();
        issues[0].ShouldBe("No file matching pattern '*.nfo' found in the release folder");
    }

    [Test]
    public void FilePattern_DirectoryMissing_ReturnsIssue()
    {
        // Arrange
        var check = new FilePatternQualityCheck();
        var rule = CreateRule(new Dictionary<string, object?> { ["pattern"] = "*.nfo" });
        var context = CreateContext(new FakeFileSystemService { DirectoryExistsResult = false });

        // Act
        var issues = check.Evaluate(rule, context);

        // Assert
        issues.ShouldHaveSingleItem();
    }

    [Test]
    public void MinimumFolderSize_FolderLargeEnough_ReturnsNoIssues()
    {
        // Arrange
        var check = new MinimumFolderSizeQualityCheck();
        var rule = CreateRule(new Dictionary<string, object?> { ["minimumMegabytes"] = 100 });
        var context = CreateContext(new FakeFileSystemService { TotalBytes = 200L * 1024 * 1024 });

        // Act
        var issues = check.Evaluate(rule, context);

        // Assert
        issues.ShouldBeEmpty();
    }

    [Test]
    public void MinimumFolderSize_FolderTooSmall_ReturnsIssue()
    {
        // Arrange
        var check = new MinimumFolderSizeQualityCheck();
        var rule = CreateRule(new Dictionary<string, object?> { ["minimumMegabytes"] = 100 });
        var context = CreateContext(new FakeFileSystemService { TotalBytes = 50L * 1024 * 1024 });

        // Act
        var issues = check.Evaluate(rule, context);

        // Assert
        issues.ShouldHaveSingleItem();
        issues[0].ShouldBe("Release folder is smaller than the required 100 MB");
    }

    [Test]
    public void RequiredReleaseInfo_AllInfoPresent_ReturnsNoIssues()
    {
        // Arrange
        var check = new RequiredReleaseInfoQualityCheck();
        var rule = CreateRule(
            new Dictionary<string, object?>
            {
                ["requireCover"] = true,
                ["requireDescription"] = true,
                ["requireNfo"] = true,
            }
        );
        var release = CreateRelease();
        release.ReleaseInfo = new ReleaseInfo
        {
            NfoDatabaseClassName = ReleaseInfo.ManualSource,
            ReleaseName = release.Name,
        };
        release.Metadata = new ReleaseMetadata
        {
            MetadataDatabaseClassName = ReleaseMetadata.ManualSource,
            Title = release.Name,
            CoverUrl = "https://images.test/cover.jpg",
            Description = "A description",
        };
        release.ReleaseNfo = new ReleaseNfo { FileName = "release.nfo", Content = "NFO body" };
        var context = CreateContext(new FakeFileSystemService(), release);

        // Act
        var issues = check.Evaluate(rule, context);

        // Assert
        issues.ShouldBeEmpty();
    }

    [Test]
    public void RequiredReleaseInfo_NoReleaseInfo_ReturnsIssuePerRequirement()
    {
        // Arrange
        var check = new RequiredReleaseInfoQualityCheck();
        var rule = CreateRule(
            new Dictionary<string, object?>
            {
                ["requireCover"] = true,
                ["requireDescription"] = true,
                ["requireNfo"] = true,
            }
        );
        var context = CreateContext(new FakeFileSystemService());

        // Act
        var issues = check.Evaluate(rule, context);

        // Assert
        issues.ShouldBe(["Cover image is missing", "Description is missing", "NFO is missing"]);
    }

    [Test]
    public void RequiredReleaseInfo_OnlyNfoRequiredAndMissing_ReturnsOnlyNfoIssue()
    {
        // Arrange
        var check = new RequiredReleaseInfoQualityCheck();
        var rule = CreateRule(
            new Dictionary<string, object?>
            {
                ["requireCover"] = false,
                ["requireDescription"] = false,
                ["requireNfo"] = true,
            }
        );
        var release = CreateRelease();
        release.ReleaseInfo = new ReleaseInfo
        {
            NfoDatabaseClassName = ReleaseInfo.ManualSource,
            ReleaseName = release.Name,
        };
        release.ReleaseNfo = new ReleaseNfo { FileName = "release.nfo", Content = "   " };
        var context = CreateContext(new FakeFileSystemService(), release);

        // Act
        var issues = check.Evaluate(rule, context);

        // Assert
        issues.ShouldBe(["NFO is missing"]);
    }

    [Test]
    public void MediaInfo_MediaFilesPresent_ReturnsNoIssues()
    {
        // Arrange
        var check = new MediaInfoQualityCheck();
        var release = CreateRelease();
        release.MediaFiles =
        [
            new ReleaseMediaFile
            {
                RelativePath = "movie.mkv",
                SizeBytes = 1,
                MediaInfoJson = "{}",
                MediaInfoText = "info",
            },
        ];
        var context = CreateContext(new FakeFileSystemService(), release);

        // Act
        var issues = check.Evaluate(CreateRule([]), context);

        // Assert
        issues.ShouldBeEmpty();
    }

    [Test]
    public void MediaInfo_NoMediaFiles_ReturnsIssue()
    {
        // Arrange
        var check = new MediaInfoQualityCheck();
        var context = CreateContext(new FakeFileSystemService());

        // Act
        var issues = check.Evaluate(CreateRule([]), context);

        // Assert
        issues.ShouldHaveSingleItem();
        issues[0].ShouldBe("No media info has been extracted");
    }

    private static QualityCheckRule CreateRule(Dictionary<string, object?> parameters) =>
        new() { ParametersJson = QualityCheckParameterValues.Serialize(parameters) };

    private static QualityCheckContext CreateContext(
        FakeFileSystemService fileSystemService,
        Release? release = null
    ) => new(release ?? CreateRelease(), fileSystemService);

    private static Release CreateRelease() =>
        new()
        {
            Name = "Bearcat.Release.001",
            ReleaseFolderPath = "/release",
            ReleaseGroup = new ReleaseGroup { Name = "Group" },
        };
}
