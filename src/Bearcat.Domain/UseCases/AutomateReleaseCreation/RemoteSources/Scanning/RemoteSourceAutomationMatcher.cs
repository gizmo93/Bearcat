using System.IO.Enumeration;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;

public static class RemoteSourceAutomationMatcher
{
    public static IReadOnlyList<RemoteSourceAutomation> OrderByPriority(
        IReadOnlyList<RemoteSourceAutomation> automations
    )
    {
        return automations
            .OrderBy(automation => automation.Priority)
            .ThenBy(automation => automation.Id)
            .ToList();
    }

    public static RemoteSourceAutomation? FindFirstMatch(
        IReadOnlyList<RemoteSourceAutomation> automations,
        string folderName
    )
    {
        return OrderByPriority(automations)
            .FirstOrDefault(automation => Matches(automation, folderName));
    }

    public static bool Matches(RemoteSourceAutomation automation, string folderName)
    {
        return string.IsNullOrWhiteSpace(automation.FolderNamePattern)
            || FileSystemName.MatchesSimpleExpression(
                automation.FolderNamePattern,
                folderName,
                ignoreCase: true
            );
    }
}
