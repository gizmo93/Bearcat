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

    public static Dictionary<int, List<RemoteFolderDto>> ResolveClaims(
        IReadOnlyList<RemoteSourceAutomation> automations,
        IReadOnlyDictionary<string, IReadOnlyList<RemoteFolderDto>> listings,
        IReadOnlyDictionary<string, RemoteSourceDownload> existingDownloadsByPath
    )
    {
        var claims = automations.ToDictionary(
            automation => automation.Id,
            _ => new List<RemoteFolderDto>()
        );

        foreach (var (remotePath, folders) in listings)
        {
            var pathAutomations = automations
                .Where(automation => automation.RemotePath == remotePath)
                .ToList();

            foreach (var folder in folders)
            {
                var owner = existingDownloadsByPath.TryGetValue(folder.FullPath, out var download)
                    ? FindExistingOwner(pathAutomations, download, folder)
                    : FindFirstMatch(pathAutomations, folder.Name);

                if (owner is not null)
                {
                    claims[owner.Id].Add(folder);
                }
            }
        }

        return claims;
    }

    public static RemoteSourceAutomation? FindExistingOwner(
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
            automation.Id == download.RemoteSourceAutomationId && Matches(automation, folder.Name)
        );
    }
}
