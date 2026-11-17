using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Shared.ForumPosting;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ForumPosting;

public class ForumThreadTitleMatcherTest
{
    private const string SearchName = "Joker 2019 German DL 1080p BluRay x265-GRP";

    [Test]
    public void FindMatchingThreads_ExactTitleAndLongerTitle_ReturnsOnlyTheExactTitle()
    {
        // Arrange
        var exact = Thread("Joker 2019 German DL 1080p BluRay x265-GRP");
        var repack = Thread("Joker 2019 German DL 1080p BluRay x265-GRP REPACK");

        // Act
        var matches = ForumThreadTitleMatcher.FindMatchingThreads(SearchName, [repack, exact]);

        // Assert
        matches.ShouldBe([exact]);
    }

    [Test]
    public void FindMatchingThreads_DifferentPunctuationAndCase_CountsAsExactTitle()
    {
        // Arrange
        var thread = Thread("joker.2019.german.dl.1080p.bluray.x265  grp");

        // Act
        var matches = ForumThreadTitleMatcher.FindMatchingThreads(SearchName, [thread]);

        // Assert
        matches.ShouldBe([thread]);
    }

    [Test]
    public void FindMatchingThreads_NoExactTitle_ReturnsTitlesWithAllWordsInOrder()
    {
        // Arrange
        var withExtraWords = Thread("Joker 2019 German DL 1080p BluRay x265-GRP [Upload] mirror");
        var unrelated = Thread("Joker Folie a Deux 2024 German DL 1080p");

        // Act
        var matches = ForumThreadTitleMatcher.FindMatchingThreads(
            SearchName,
            [withExtraWords, unrelated]
        );

        // Assert
        matches.ShouldBe([withExtraWords]);
    }

    [Test]
    public void FindMatchingThreads_TwoTitlesWithAllWords_ReturnsBoth()
    {
        // Arrange
        var repack = Thread("Joker 2019 German DL 1080p BluRay x265-GRP REPACK");
        var proper = Thread("Joker 2019 German DL 1080p BluRay x265-GRP PROPER");

        // Act
        var matches = ForumThreadTitleMatcher.FindMatchingThreads(SearchName, [repack, proper]);

        // Assert
        matches.ShouldBe([repack, proper]);
    }

    [Test]
    public void FindMatchingThreads_WordsInDifferentOrder_ReturnsNoMatch()
    {
        // Arrange
        var thread = Thread("Joker German 2019 DL 1080p BluRay x265-GRP");

        // Act
        var matches = ForumThreadTitleMatcher.FindMatchingThreads(SearchName, [thread]);

        // Assert
        matches.ShouldBeEmpty();
    }

    [Test]
    public void FindMatchingThreads_WordOnlyAsPartOfALongerWord_ReturnsNoMatch()
    {
        // Arrange
        var thread = Thread("Jokers 2019 German DL 1080p BluRay x265-GRP");

        // Act
        var matches = ForumThreadTitleMatcher.FindMatchingThreads(SearchName, [thread]);

        // Assert
        matches.ShouldBeEmpty();
    }

    private static ExistingThread Thread(string title)
    {
        return new ExistingThread(
            title,
            $"https://forum.test/threads/{Uri.EscapeDataString(title)}"
        );
    }
}
