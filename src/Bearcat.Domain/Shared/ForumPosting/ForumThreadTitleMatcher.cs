using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Domain.Shared.ForumPosting;

public static class ForumThreadTitleMatcher
{
    public static List<ExistingThread> FindMatchingThreads(
        string searchName,
        IReadOnlyList<ExistingThread> threads
    )
    {
        var searchWords = SplitIntoLowercaseWords(searchName);

        var threadsWithWords = threads
            .Select(thread => (Thread: thread, Words: SplitIntoLowercaseWords(thread.Title)))
            .ToList();

        var exactMatches = threadsWithWords
            .Where(entry => entry.Words.SequenceEqual(searchWords))
            .Select(entry => entry.Thread)
            .ToList();

        if (exactMatches.Count > 0)
        {
            return exactMatches;
        }

        return threadsWithWords
            .Where(entry => ContainsWordsInOrder(entry.Words, searchWords))
            .Select(entry => entry.Thread)
            .ToList();
    }

    private static List<string> SplitIntoLowercaseWords(string value)
    {
        var characters = value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();

        return new string(characters).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static bool ContainsWordsInOrder(List<string> words, List<string> searchWords)
    {
        var searchWordIndex = 0;

        foreach (var word in words)
        {
            if (word == searchWords[searchWordIndex])
            {
                searchWordIndex++;

                if (searchWordIndex == searchWords.Count)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
