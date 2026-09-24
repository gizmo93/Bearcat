using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;

public class RemoteSourceAutomationMatcherTest
{
    private const string RemotePath = "/incoming";

    [Test]
    public void FindFirstMatchingAutomation_SeveralAutomationsMatch_ReturnsLowestPriority()
    {
        // Arrange
        var catchAll = CreateAutomation(id: 1, priority: 200, pattern: null);
        var german = CreateAutomation(id: 2, priority: 100, pattern: "*.German.*");
        var hd = CreateAutomation(id: 3, priority: 150, pattern: "*1080p*");

        // Act
        var automation = RemoteSourceAutomationMatcher.FindFirstMatchingAutomation(
            [catchAll, hd, german],
            "Show.S01E01.German.1080p.WEB.x264-GRP"
        );

        // Assert
        automation.ShouldBeSameAs(german);
    }

    [Test]
    public void FindFirstMatchingAutomation_SamePriority_ReturnsLowestId()
    {
        // Arrange
        var later = CreateAutomation(id: 7, priority: 100, pattern: null);
        var earlier = CreateAutomation(id: 3, priority: 100, pattern: null);

        // Act
        var automation = RemoteSourceAutomationMatcher.FindFirstMatchingAutomation(
            [later, earlier],
            "Any"
        );

        // Assert
        automation.ShouldBeSameAs(earlier);
    }

    [Test]
    public void FindFirstMatchingAutomation_PatternDiffersInCase_MatchesIgnoringCase()
    {
        // Arrange
        var hd = CreateAutomation(id: 1, priority: 100, pattern: "*1080P*");

        // Act
        var automation = RemoteSourceAutomationMatcher.FindFirstMatchingAutomation(
            [hd],
            "show.1080p.web"
        );

        // Assert
        automation.ShouldBeSameAs(hd);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void FindFirstMatchingAutomation_EmptyPattern_MatchesEveryFolder(string? pattern)
    {
        // Arrange
        var catchAll = CreateAutomation(id: 1, priority: 100, pattern: pattern);

        // Act
        var automation = RemoteSourceAutomationMatcher.FindFirstMatchingAutomation(
            [catchAll],
            "Anything"
        );

        // Assert
        automation.ShouldBeSameAs(catchAll);
    }

    [Test]
    public void FindFirstMatchingAutomation_NoAutomationMatches_ReturnsNull()
    {
        // Arrange
        var hd = CreateAutomation(id: 1, priority: 100, pattern: "*1080p*");

        // Act
        var automation = RemoteSourceAutomationMatcher.FindFirstMatchingAutomation(
            [hd],
            "Show.720p.WEB"
        );

        // Assert
        automation.ShouldBeNull();
    }

    [Test]
    public void AssignFoldersToAutomations_ObservingDownloadOfLowerPriorityAutomation_KeepsFolderWithAutomationOfObservingDownload()
    {
        // Arrange
        var preferred = CreateAutomation(id: 1, priority: 100, pattern: null);
        var automation = CreateAutomation(id: 2, priority: 200, pattern: null);
        var folder = CreateFolder("Show.S01E01.German.1080p.WEB.x264-GRP");
        var download = CreateDownload(folder, automation.Id, RemoteSourceDownloadState.Observing);

        // Act
        var foldersByAutomationId = RemoteSourceAutomationMatcher.AssignFoldersToAutomations(
            [preferred, automation],
            CreateFoldersByRemotePath(folder),
            new Dictionary<string, RemoteSourceDownload> { [folder.FullPath] = download }
        );

        // Assert
        foldersByAutomationId[automation.Id].ShouldBe([folder]);
        foldersByAutomationId[preferred.Id].ShouldBeEmpty();
    }

    [TestCase(RemoteSourceDownloadState.Pending)]
    [TestCase(RemoteSourceDownloadState.Downloading)]
    [TestCase(RemoteSourceDownloadState.Ignored)]
    public void AssignFoldersToAutomations_ExistingDownloadNotObserving_IsAssignedToNobody(
        RemoteSourceDownloadState state
    )
    {
        // Arrange
        var automation = CreateAutomation(id: 1, priority: 100, pattern: null);
        var other = CreateAutomation(id: 2, priority: 200, pattern: null);
        var folder = CreateFolder("Show.S01E01.German.1080p.WEB.x264-GRP");
        var download = CreateDownload(folder, automation.Id, state);

        // Act
        var foldersByAutomationId = RemoteSourceAutomationMatcher.AssignFoldersToAutomations(
            [automation, other],
            CreateFoldersByRemotePath(folder),
            new Dictionary<string, RemoteSourceDownload> { [folder.FullPath] = download }
        );

        // Assert
        foldersByAutomationId[automation.Id].ShouldBeEmpty();
        foldersByAutomationId[other.Id].ShouldBeEmpty();
    }

    [Test]
    public void AssignFoldersToAutomations_NewFolder_IsAssignedToFirstMatchByPriorityThenId()
    {
        // Arrange
        var hd = CreateAutomation(id: 1, priority: 50, pattern: "*1080p*");
        var later = CreateAutomation(id: 5, priority: 100, pattern: null);
        var earlier = CreateAutomation(id: 2, priority: 100, pattern: null);
        var folder = CreateFolder("Show.S01E01.German.720p.WEB.x264-GRP");

        // Act
        var foldersByAutomationId = RemoteSourceAutomationMatcher.AssignFoldersToAutomations(
            [hd, later, earlier],
            CreateFoldersByRemotePath(folder),
            new Dictionary<string, RemoteSourceDownload>()
        );

        // Assert
        foldersByAutomationId[earlier.Id].ShouldBe([folder]);
        foldersByAutomationId[later.Id].ShouldBeEmpty();
        foldersByAutomationId[hd.Id].ShouldBeEmpty();
    }

    [Test]
    public void AssignFoldersToAutomations_FolderListedForOtherRemotePath_IsNotAssigned()
    {
        // Arrange
        var automation = CreateAutomation(id: 1, priority: 100, pattern: null, "/archive");
        var folder = CreateFolder("Show.S01E01.German.720p.WEB.x264-GRP");

        // Act
        var foldersByAutomationId = RemoteSourceAutomationMatcher.AssignFoldersToAutomations(
            [automation],
            CreateFoldersByRemotePath(folder),
            new Dictionary<string, RemoteSourceDownload>()
        );

        // Assert
        foldersByAutomationId[automation.Id].ShouldBeEmpty();
    }

    private static RemoteFolderDto CreateFolder(string name)
    {
        return new RemoteFolderDto(name, $"{RemotePath}/{name}", ModifiedAt: null);
    }

    private static Dictionary<string, IReadOnlyList<RemoteFolderDto>> CreateFoldersByRemotePath(
        RemoteFolderDto folder
    )
    {
        return new Dictionary<string, IReadOnlyList<RemoteFolderDto>> { [RemotePath] = [folder] };
    }

    private static RemoteSourceDownload CreateDownload(
        RemoteFolderDto folder,
        int automationId,
        RemoteSourceDownloadState state
    )
    {
        return new RemoteSourceDownload
        {
            RemoteSourceAutomationId = automationId,
            SourceName = "Source",
            RemoteFolderPath = folder.FullPath,
            FolderName = folder.Name,
            LocalFolderPath = $"/data/{folder.Name}",
            State = state,
        };
    }

    private static RemoteSourceAutomation CreateAutomation(
        int id,
        int priority,
        string? pattern,
        string remotePath = RemotePath
    )
    {
        return new RemoteSourceAutomation
        {
            Id = id,
            Name = $"Automation {id}",
            RemotePath = remotePath,
            TargetPath = "/data",
            Priority = priority,
            FolderNamePattern = pattern,
        };
    }
}
