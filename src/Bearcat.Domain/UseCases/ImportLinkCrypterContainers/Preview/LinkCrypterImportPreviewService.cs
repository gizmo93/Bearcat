using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage.Repositories;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public class LinkCrypterImportPreviewService(
    ILinkCrypterImportRepository repository,
    IReleaseWriteRepository releaseWriteRepository,
    IReleaseFolderUsageRepository releaseFolderUsageRepository,
    IHosterFactory hosterFactory,
    ISecretProtector secretProtector,
    IFileSystemService fileSystemService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<LinkCrypterImportPreviewService> logger
)
{
    public async Task CheckFilesAndCreatePreviewAsync(
        LinkCrypterImport linkCrypterImport,
        CancellationToken cancellationToken
    )
    {
        var containers = linkCrypterImport
            .Containers.Where(container =>
                container.Status is LinkCrypterImportContainerStatus.WaitingForFileCheck
            )
            .OrderBy(container => container.Id)
            .ToList();

        if (!await CheckFilesAsync(linkCrypterImport, containers, cancellationToken))
        {
            return;
        }

        linkCrypterImport.StartProgress(containers.Count);
        await repository.SaveChangesAsync(cancellationToken);

        await CreatePreviewsAsync(linkCrypterImport, cancellationToken);

        linkCrypterImport.ProcessedContainerCount = containers.Count;

        var importableContainerCount = linkCrypterImport.Containers.Count(container =>
            container.Status is LinkCrypterImportContainerStatus.Importable
        );

        linkCrypterImport.ChangeState(
            LinkCrypterImportState.ReadyForReview,
            timeProvider.GetLocalNow()
        );

        notificationService.Create(
            kind: NotificationKind.LinkCrypterImportReadyForReview,
            message: $"Link crypter import {linkCrypterImport.Id} is ready for review: {importableContainerCount} of {linkCrypterImport.Containers.Count} containers can be imported."
        );
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task CreatePreviewsAsync(
        LinkCrypterImport linkCrypterImport,
        CancellationToken cancellationToken
    )
    {
        var containers = linkCrypterImport
            .Containers.Where(container =>
                container.Status
                    is LinkCrypterImportContainerStatus.WaitingForFileCheck
                        or LinkCrypterImportContainerStatus.Importable
                        or LinkCrypterImportContainerStatus.NotImportable
            )
            .OrderBy(container => container.Id)
            .ToList();

        var releaseTemplate =
            await releaseWriteRepository.GetTemplateForReleaseCreationOrDefaultAsync(
                linkCrypterImport.ReleaseTemplateId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Release template {linkCrypterImport.ReleaseTemplateId} of link crypter import {linkCrypterImport.Id} was not found."
            );

        var templateHosterClassNames = releaseTemplate
            .UploadConfigTemplates.Where(template =>
                template.UploadSource is UploadSource.ArchiveFiles
            )
            .OrderBy(template => template.Id)
            .Select(template => template.GetHosterClassName())
            .ToList();

        var releaseFolderBasePaths =
            releaseTemplate.ReleaseType is ReleaseType.Managed
                ? await GetReleaseFolderBasePathsAsync(cancellationToken)
                : [];

        var existingReleasesByName = await GetExistingReleasesByNameAsync(
            containers,
            cancellationToken
        );

        var archivePartFileNamesByFolderPath = await GetArchivePartFileNamesByFolderPathAsync(
            linkCrypterImport,
            cancellationToken
        );

        var releaseNamesOfEarlierContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var releaseNamesByChosenArchiveFolderPath = new Dictionary<string, string>(
            StringComparer.Ordinal
        );

        foreach (var container in containers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var notImportableReason = await CreatePreviewAsync(
                container: container,
                existingRelease: existingReleasesByName.GetValueOrDefault(container.ReleaseName),
                isNameOfEarlierContainer: releaseNamesOfEarlierContainers.Contains(
                    container.ReleaseName
                ),
                templateHosterClassNames: templateHosterClassNames,
                releaseFolderBasePaths: releaseFolderBasePaths,
                archivePartFileNamesByFolderPath: archivePartFileNamesByFolderPath,
                releaseNamesByChosenArchiveFolderPath: releaseNamesByChosenArchiveFolderPath,
                cancellationToken: cancellationToken
            );

            releaseNamesOfEarlierContainers.Add(container.ReleaseName);

            container.IsSelected =
                notImportableReason is null
                && (
                    container.Status is LinkCrypterImportContainerStatus.WaitingForFileCheck
                    || container.IsSelected
                );
            container.NotImportableReason = notImportableReason;

            container.Status = notImportableReason is null
                ? LinkCrypterImportContainerStatus.Importable
                : LinkCrypterImportContainerStatus.NotImportable;
        }
    }

    private async Task<bool> CheckFilesAsync(
        LinkCrypterImport linkCrypterImport,
        List<LinkCrypterImportContainer> containers,
        CancellationToken cancellationToken
    )
    {
        if (linkCrypterImport.HosterRegistrations.Any(choice => choice.HosterRegistration is null))
        {
            await ReturnToHosterRegistrationChoiceAsync(
                linkCrypterImport: linkCrypterImport,
                errorMessages:
                [
                    "A hoster account must be chosen for every hoster before the files are checked.",
                ],
                cancellationToken: cancellationToken
            );
            return false;
        }

        var previewsByContainer = containers.ToDictionary(
            container => container,
            container => LinkCrypterImportContainerPreview.FromJson(container.PreviewJson)
        );

        var fileInfosPerLink = new Dictionary<string, HostedFileInfo>(StringComparer.Ordinal);
        var errorMessages = new List<string>();

        foreach (var registration in linkCrypterImport.HosterRegistrations)
        {
            var links = previewsByContainer
                .Values.SelectMany(preview => preview.Mirrors)
                .Where(mirror => mirror.HosterClassName == registration.HosterClassName)
                .SelectMany(mirror => mirror.Files)
                .Select(file => file.Link)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (links.Count == 0)
            {
                continue;
            }

            var result = await GetFileInfosAsync(
                hosterClassName: registration.HosterClassName,
                registration: registration.HosterRegistration!,
                links: links,
                cancellationToken: cancellationToken
            );

            if (!result.IsSuccess)
            {
                errorMessages.Add(
                    $"Checking the files with hoster account {registration.HosterRegistration!.Name} failed: {string.Join("; ", result.ErrorMessages)}"
                );
                continue;
            }

            foreach (var (link, fileInfo) in result.FileInfoPerFileLink)
            {
                fileInfosPerLink[link] = fileInfo;
            }
        }

        if (errorMessages.Count > 0)
        {
            await ReturnToHosterRegistrationChoiceAsync(
                linkCrypterImport: linkCrypterImport,
                errorMessages: errorMessages,
                cancellationToken: cancellationToken
            );
            return false;
        }

        foreach (var (container, preview) in previewsByContainer)
        {
            var checkedMirrors = preview
                .Mirrors.Select(mirror => AddFileInfos(mirror, fileInfosPerLink))
                .ToList();

            container.PreviewJson = (preview with { Mirrors = checkedMirrors }).ToJson();
        }

        linkCrypterImport.ErrorMessages = [];

        logger.LogInformation(
            "Checked {LinkCount} file links of link crypter import {LinkCrypterImportId}",
            fileInfosPerLink.Count,
            linkCrypterImport.Id
        );

        return true;
    }

    private async Task<FileInfoResult> GetFileInfosAsync(
        string hosterClassName,
        HosterRegistration registration,
        IReadOnlyList<string> links,
        CancellationToken cancellationToken
    )
    {
        var hoster = (IHosterWithFileInfo)hosterFactory.GetByName(hosterClassName);

        try
        {
            var hosterConfig = hoster.DeserializeHosterConfig(
                secretProtector.Unprotect(registration.SerializedConfig)
            );

            using var proxyScope = HosterRegistrationProxyScope.EnterForUpload(registration);

            return await hoster.GetFileInfosAsync(hosterConfig, links, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Could not check the files of link crypter import with hoster account {HosterRegistrationName}",
                registration.Name
            );

            return new FileInfoResult(
                IsSuccess: false,
                ErrorMessages: [exception.Message],
                FileInfoPerFileLink: new Dictionary<string, HostedFileInfo>()
            );
        }
    }

    private static LinkCrypterImportMirror AddFileInfos(
        LinkCrypterImportMirror mirror,
        Dictionary<string, HostedFileInfo> fileInfosPerLink
    )
    {
        var files = mirror
            .Files.Select(file =>
                fileInfosPerLink.TryGetValue(file.Link, out var fileInfo)
                    ? file with
                    {
                        IsOnline = fileInfo.IsOnline,
                        FileName = fileInfo.FileName,
                        SizeBytes = fileInfo.SizeBytes,
                    }
                    : file
            )
            .ToList();

        return mirror with
        {
            Files = files,
        };
    }

    private async Task ReturnToHosterRegistrationChoiceAsync(
        LinkCrypterImport linkCrypterImport,
        IReadOnlyList<string> errorMessages,
        CancellationToken cancellationToken
    )
    {
        linkCrypterImport.ChangeState(
            LinkCrypterImportState.WaitingForHosterRegistrations,
            timeProvider.GetLocalNow()
        );

        linkCrypterImport.ErrorMessages = errorMessages.ToList();

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Link crypter import {LinkCrypterImportId} waits for new hoster accounts: {Errors}",
            linkCrypterImport.Id,
            string.Join("; ", errorMessages)
        );
    }

    private async Task<Dictionary<string, Release>> GetExistingReleasesByNameAsync(
        List<LinkCrypterImportContainer> containers,
        CancellationToken cancellationToken
    )
    {
        var releases = await repository.GetReleasesByNameAsync(
            containers.Select(container => container.ReleaseName).Distinct().ToList(),
            cancellationToken
        );

        return releases
            .DistinctBy(release => release.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(release => release.Name, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<string>> GetReleaseFolderBasePathsAsync(
        CancellationToken cancellationToken
    )
    {
        var releaseFolderAutomationBasePaths =
            await repository.GetReleaseFolderAutomationBasePathsAsync(cancellationToken);

        return workingDirectoriesConfig
            .Value.GetWorkingDirectories()
            .Concat(releaseFolderAutomationBasePaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.TrimEndingDirectorySeparator)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<LinkCrypterImportNotImportableReason?> CreatePreviewAsync(
        LinkCrypterImportContainer container,
        Release? existingRelease,
        bool isNameOfEarlierContainer,
        List<string> templateHosterClassNames,
        List<string> releaseFolderBasePaths,
        Dictionary<string, HashSet<string>> archivePartFileNamesByFolderPath,
        Dictionary<string, string> releaseNamesByChosenArchiveFolderPath,
        CancellationToken cancellationToken
    )
    {
        var releaseAction = GetReleaseAction(existingRelease, isNameOfEarlierContainer);

        var mirrors = DecideMirrors(
            mirrors: LinkCrypterImportContainerPreview.FromJson(container.PreviewJson).Mirrors,
            hosterClassNames: existingRelease is null
                ? templateHosterClassNames
                : GetArchiveFilesHosterClassNames(existingRelease)
        );

        var releaseFolderPath = existingRelease is null
            ? await FindUnusedReleaseFolderPathAsync(
                container.ReleaseName,
                releaseFolderBasePaths,
                cancellationToken
            )
            : null;

        var archiveFolderPath = existingRelease is null
            ? await FindUnusedArchiveFolderPathAsync(
                releaseName: container.ReleaseName,
                mirrors: mirrors,
                archivePartFileNamesByFolderPath: archivePartFileNamesByFolderPath,
                releaseNamesByChosenArchiveFolderPath: releaseNamesByChosenArchiveFolderPath,
                cancellationToken: cancellationToken
            )
            : null;

        if (archiveFolderPath is not null)
        {
            releaseNamesByChosenArchiveFolderPath.TryAdd(archiveFolderPath, container.ReleaseName);
        }

        if (archiveFolderPath is not null && archiveFolderPath == releaseFolderPath)
        {
            releaseFolderPath = null;
        }

        container.PreviewJson = new LinkCrypterImportContainerPreview(
            Mirrors: mirrors,
            ReleaseFolderPath: releaseFolderPath,
            ArchiveFolderPath: archiveFolderPath,
            ReleaseAction: releaseAction,
            ExistingReleaseId: existingRelease?.Id
        ).ToJson();

        return GetNotImportableReason(
            mirrors: mirrors,
            releaseAction: releaseAction,
            releaseFolderPath: releaseFolderPath,
            archiveFolderPath: archiveFolderPath,
            archiveFilesBasePath: container
                .LinkCrypterImport
                .ArchiveFilesBasePathForReleasesWithoutLocalFiles
        );
    }

    private static LinkCrypterImportReleaseAction GetReleaseAction(
        Release? existingRelease,
        bool isNameOfEarlierContainer
    )
    {
        if (existingRelease is not null)
        {
            return LinkCrypterImportReleaseAction.AttachToExistingRelease;
        }

        return isNameOfEarlierContainer
            ? LinkCrypterImportReleaseAction.AttachToReleaseOfEarlierContainer
            : LinkCrypterImportReleaseAction.CreateRelease;
    }

    private static List<string> GetArchiveFilesHosterClassNames(Release release)
    {
        return release
            .UploadConfigs.Where(uploadConfig =>
                uploadConfig.UploadSource is UploadSource.ArchiveFiles
            )
            .OrderBy(uploadConfig => uploadConfig.Id)
            .Select(uploadConfig => uploadConfig.GetHosterClassName())
            .ToList();
    }

    private async Task<string?> FindUnusedReleaseFolderPathAsync(
        string releaseName,
        List<string> releaseFolderBasePaths,
        CancellationToken cancellationToken
    )
    {
        var existingFolderPaths = releaseFolderBasePaths
            .Select(basePath => Path.Combine(basePath, releaseName))
            .Where(fileSystemService.DirectoryExists)
            .ToList();

        if (existingFolderPaths.Count == 0)
        {
            return null;
        }

        var usedFolderPaths =
            await releaseFolderUsageRepository.GetUsedReleaseArchiveOrRemoteDownloadFolderPathsAsync(
                existingFolderPaths,
                cancellationToken
            );

        return existingFolderPaths.FirstOrDefault(folderPath =>
            !usedFolderPaths.Contains(folderPath)
        );
    }

    private async Task<
        Dictionary<string, HashSet<string>>
    > GetArchivePartFileNamesByFolderPathAsync(
        LinkCrypterImport linkCrypterImport,
        CancellationToken cancellationToken
    )
    {
        var archiveStorageFolders = await repository.GetArchiveStorageFoldersAsync(
            cancellationToken
        );

        List<string?> searchFolderPathCandidates =
        [
            .. archiveStorageFolders.Select(storageFolder => storageFolder.Path),
            .. workingDirectoriesConfig.Value.GetWorkingDirectories(),
            linkCrypterImport.ArchiveFilesBasePathForReleasesWithoutLocalFiles,
        ];

        var searchFolderPaths = searchFolderPathCandidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.TrimEndingDirectorySeparator(path!))
            .Distinct(StringComparer.Ordinal)
            .Where(fileSystemService.DirectoryExists)
            .ToList();

        var folderPaths = searchFolderPaths
            .SelectMany(searchFolderPath =>
                fileSystemService.GetFoldersInPath(searchFolderPath).Prepend(searchFolderPath)
            )
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var archivePartFileNamesByFolderPath = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal
        );

        foreach (var folderPath in folderPaths)
        {
            var archivePartFileNames = fileSystemService
                .GetFilesInPath(folderPath, recursive: false)
                .Select(filePath => Path.GetFileName(filePath))
                .Where(ArchivePartFileNames.IsArchivePart)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (archivePartFileNames.Count > 0)
            {
                archivePartFileNamesByFolderPath[folderPath] = archivePartFileNames;
            }
        }

        return archivePartFileNamesByFolderPath;
    }

    private async Task<string?> FindUnusedArchiveFolderPathAsync(
        string releaseName,
        List<LinkCrypterImportMirror> mirrors,
        Dictionary<string, HashSet<string>> archivePartFileNamesByFolderPath,
        Dictionary<string, string> releaseNamesByChosenArchiveFolderPath,
        CancellationToken cancellationToken
    )
    {
        var firstAdoptedMirror = mirrors.FirstOrDefault(mirror =>
            mirror.Decision is LinkCrypterImportMirrorDecision.Adopt
        );

        if (firstAdoptedMirror is null)
        {
            return null;
        }

        var fileNames = firstAdoptedMirror.Files.Select(file => file.FileName!).ToList();

        var folderPathsWithSameArchiveParts = archivePartFileNamesByFolderPath
            .Where(entry => entry.Value.SetEquals(fileNames))
            .Select(entry => entry.Key)
            .Where(folderPath =>
                !IsArchiveFolderChosenForOtherRelease(
                    folderPath: folderPath,
                    releaseName: releaseName,
                    releaseNamesByChosenArchiveFolderPath: releaseNamesByChosenArchiveFolderPath
                )
            )
            .Order(StringComparer.Ordinal)
            .ToList();

        if (folderPathsWithSameArchiveParts.Count == 0)
        {
            return null;
        }

        var usedFolderPaths =
            await releaseFolderUsageRepository.GetUsedReleaseArchiveOrRemoteDownloadFolderPathsAsync(
                folderPathsWithSameArchiveParts,
                cancellationToken
            );

        return folderPathsWithSameArchiveParts.FirstOrDefault(folderPath =>
            !usedFolderPaths.Contains(folderPath)
        );
    }

    private static bool IsArchiveFolderChosenForOtherRelease(
        string folderPath,
        string releaseName,
        Dictionary<string, string> releaseNamesByChosenArchiveFolderPath
    )
    {
        var chosenReleaseName = releaseNamesByChosenArchiveFolderPath.GetValueOrDefault(folderPath);

        return chosenReleaseName is not null
            && !string.Equals(chosenReleaseName, releaseName, StringComparison.OrdinalIgnoreCase);
    }

    private static List<LinkCrypterImportMirror> DecideMirrors(
        IReadOnlyList<LinkCrypterImportMirror> mirrors,
        List<string> hosterClassNames
    )
    {
        var unusedHosterClassNames = hosterClassNames.ToList();
        var decidedMirrors = new List<LinkCrypterImportMirror>();

        foreach (var mirror in mirrors.OrderBy(mirror => mirror.MirrorNumber))
        {
            if (mirror.HosterClassName is null)
            {
                decidedMirrors.Add(
                    mirror with
                    {
                        Decision = LinkCrypterImportMirrorDecision.UnsupportedHoster,
                        UploadByBearcatReason = null,
                    }
                );
                continue;
            }

            if (!unusedHosterClassNames.Remove(mirror.HosterClassName))
            {
                decidedMirrors.Add(
                    mirror with
                    {
                        Decision = LinkCrypterImportMirrorDecision.HosterNotInRelease,
                        UploadByBearcatReason = null,
                    }
                );
                continue;
            }

            var uploadByBearcatReason = GetUploadByBearcatReason(
                mirror: mirror,
                firstAdoptedMirror: decidedMirrors.FirstOrDefault(decidedMirror =>
                    decidedMirror.Decision is LinkCrypterImportMirrorDecision.Adopt
                )
            );

            decidedMirrors.Add(
                mirror with
                {
                    Decision = uploadByBearcatReason is null
                        ? LinkCrypterImportMirrorDecision.Adopt
                        : LinkCrypterImportMirrorDecision.UploadByBearcat,
                    UploadByBearcatReason = uploadByBearcatReason,
                }
            );
        }

        return decidedMirrors;
    }

    private static LinkCrypterImportUploadByBearcatReason? GetUploadByBearcatReason(
        LinkCrypterImportMirror mirror,
        LinkCrypterImportMirror? firstAdoptedMirror
    )
    {
        if (mirror.Files.Any(file => file.IsOnline is false))
        {
            return LinkCrypterImportUploadByBearcatReason.SomeFilesOffline;
        }

        if (
            mirror.Files.Any(file =>
                file.IsOnline is null || file.FileName is null || file.OwnFormatLink is null
            )
        )
        {
            return LinkCrypterImportUploadByBearcatReason.FileStatesUnknown;
        }

        if (mirror.Files.Any(file => !ArchivePartFileNames.IsArchivePart(file.FileName!)))
        {
            return LinkCrypterImportUploadByBearcatReason.FileNamesAreNotArchiveParts;
        }

        if (firstAdoptedMirror is not null && !HaveSameFileNames(mirror, firstAdoptedMirror))
        {
            return LinkCrypterImportUploadByBearcatReason.FileNamesDifferFromFirstAdoptedMirror;
        }

        return null;
    }

    private static bool HaveSameFileNames(
        LinkCrypterImportMirror mirror,
        LinkCrypterImportMirror otherMirror
    )
    {
        return mirror
            .Files.Select(file => file.FileName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(otherMirror.Files.Select(file => file.FileName!));
    }

    private static LinkCrypterImportNotImportableReason? GetNotImportableReason(
        List<LinkCrypterImportMirror> mirrors,
        LinkCrypterImportReleaseAction releaseAction,
        string? releaseFolderPath,
        string? archiveFolderPath,
        string? archiveFilesBasePath
    )
    {
        if (
            !mirrors.Any(mirror =>
                mirror.Decision
                    is LinkCrypterImportMirrorDecision.Adopt
                        or LinkCrypterImportMirrorDecision.UploadByBearcat
            )
        )
        {
            return LinkCrypterImportNotImportableReason.NoMirrorForReleaseHoster;
        }

        if (
            releaseAction is not LinkCrypterImportReleaseAction.CreateRelease
            || releaseFolderPath is not null
            || archiveFolderPath is not null
        )
        {
            return null;
        }

        if (!mirrors.Any(mirror => mirror.Decision is LinkCrypterImportMirrorDecision.Adopt))
        {
            return LinkCrypterImportNotImportableReason.NoAdoptedMirrorAndNoReleaseFolder;
        }

        if (archiveFilesBasePath is null)
        {
            return LinkCrypterImportNotImportableReason.ArchiveFilesBasePathMissing;
        }

        return null;
    }
}
