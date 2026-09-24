using System.IO.Enumeration;
using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

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

    public static RemoteSourceAutomation? FindFirstMatchingAutomation(
        IReadOnlyList<RemoteSourceAutomation> automations,
        string folderName
    )
    {
        return OrderByPriority(automations)
            .FirstOrDefault(automation => MatchesFolderName(automation, folderName));
    }

    public static bool MatchesFolderName(RemoteSourceAutomation automation, string folderName)
    {
        return string.IsNullOrWhiteSpace(automation.FolderNamePattern)
            || FileSystemName.MatchesSimpleExpression(
                automation.FolderNamePattern,
                folderName,
                ignoreCase: true
            );
    }

    public static Dictionary<int, List<RemoteFolderDto>> AssignFoldersToAutomations(
        IReadOnlyList<RemoteSourceAutomation> automations,
        IReadOnlyDictionary<string, IReadOnlyList<RemoteFolderDto>> foldersByRemotePath,
        IReadOnlyDictionary<string, RemoteSourceDownload> existingDownloadsByPath
    )
    {
        var foldersByAutomationId = automations.ToDictionary(
            automation => automation.Id,
            _ => new List<RemoteFolderDto>()
        );

        foreach (var (remotePath, folders) in foldersByRemotePath)
        {
            var pathAutomations = automations
                .Where(automation => automation.RemotePath == remotePath)
                .ToList();

            foreach (var folder in folders)
            {
                var automation = existingDownloadsByPath.TryGetValue(
                    folder.FullPath,
                    out var download
                )
                    ? FindAutomationOfObservingDownload(pathAutomations, download, folder)
                    : FindFirstMatchingAutomation(pathAutomations, folder.Name);

                if (automation is not null)
                {
                    foldersByAutomationId[automation.Id].Add(folder);
                }
            }
        }

        return foldersByAutomationId;
    }

    public static RemoteSourceAutomation? FindAutomationOfObservingDownload(
        IReadOnlyList<RemoteSourceAutomation> pathAutomations,
        RemoteSourceDownload download,
        RemoteFolderDto folder
    )
    {
        if (download.State is not RemoteSourceDownloadState.Observing)
        {
            return null;
        }

        return pathAutomations.FirstOrDefault(automation =>
            automation.Id == download.RemoteSourceAutomationId
            && MatchesFolderName(automation, folder.Name)
        );
    }
}
