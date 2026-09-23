using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;

public class RemoteSourceAutomationMatcherTest
{
    [Test]
    public void FindFirstMatch_SeveralAutomationsMatch_ReturnsLowestPriority()
    {
        // Arrange
        var catchAll = CreateAutomation(id: 1, priority: 200, pattern: null);
        var german = CreateAutomation(id: 2, priority: 100, pattern: "*.German.*");
        var hd = CreateAutomation(id: 3, priority: 150, pattern: "*1080p*");

        // Act
        var automation = RemoteSourceAutomationMatcher.FindFirstMatch(
            [catchAll, hd, german],
            "Show.S01E01.German.1080p.WEB.x264-GRP"
        );

        // Assert
        automation.ShouldBeSameAs(german);
    }

    [Test]
    public void FindFirstMatch_SamePriority_ReturnsLowestId()
    {
        // Arrange
        var later = CreateAutomation(id: 7, priority: 100, pattern: null);
        var earlier = CreateAutomation(id: 3, priority: 100, pattern: null);

        // Act
        var automation = RemoteSourceAutomationMatcher.FindFirstMatch([later, earlier], "Any");

        // Assert
        automation.ShouldBeSameAs(earlier);
    }

    [Test]
    public void FindFirstMatch_PatternDiffersInCase_MatchesIgnoringCase()
    {
        // Arrange
        var hd = CreateAutomation(id: 1, priority: 100, pattern: "*1080P*");

        // Act
        var automation = RemoteSourceAutomationMatcher.FindFirstMatch([hd], "show.1080p.web");

        // Assert
        automation.ShouldBeSameAs(hd);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void FindFirstMatch_EmptyPattern_MatchesEveryFolder(string? pattern)
    {
        // Arrange
        var catchAll = CreateAutomation(id: 1, priority: 100, pattern: pattern);

        // Act
        var automation = RemoteSourceAutomationMatcher.FindFirstMatch([catchAll], "Anything");

        // Assert
        automation.ShouldBeSameAs(catchAll);
    }

    [Test]
    public void FindFirstMatch_NoAutomationMatches_ReturnsNull()
    {
        // Arrange
        var hd = CreateAutomation(id: 1, priority: 100, pattern: "*1080p*");

        // Act
        var automation = RemoteSourceAutomationMatcher.FindFirstMatch([hd], "Show.720p.WEB");

        // Assert
        automation.ShouldBeNull();
    }

    private static RemoteSourceAutomation CreateAutomation(int id, int priority, string? pattern)
    {
        return new RemoteSourceAutomation
        {
            Id = id,
            Name = $"Automation {id}",
            RemotePath = "/incoming",
            TargetPath = "/data",
            Priority = priority,
            FolderNamePattern = pattern,
        };
    }
}
