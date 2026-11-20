using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage.Repositories;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
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

        var releaseTemplate = await GetReleaseTemplateAsync(linkCrypterImport, cancellationToken);
        var templateHosterClassNames = GetTemplateHosterClassNames(releaseTemplate);

        var releaseFolderBasePaths = await GetReleaseFolderBasePathsAsync(
            releaseTemplate,
            cancellationToken
        );

        var mirrorsByContainer = containers.ToDictionary(
            container => container,
            GetMirrorsWithoutDecisions
        );

        var mirrorsOfArchivesByContainer = mirrorsByContainer.ToDictionary(
            entry => entry.Key,
            entry => LinkCrypterImportMirrorSplitter.SplitByArchiveBaseName(entry.Value)
        );

        var existingReleasesByName = await GetExistingReleasesByNameAsync(
            mirrorsOfArchivesByContainer
                .SelectMany(entry => GetReleaseNames(entry.Key, entry.Value))
                .ToList(),
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

            var mirrorsOfArchives = mirrorsOfArchivesByContainer[container];

            var previewResult = await CreatePreviewAsync(
                container: container,
                mirrors: mirrorsByContainer[container],
                mirrorsOfArchives: mirrorsOfArchives,
                releaseTemplate: releaseTemplate,
                existingReleasesByName: existingReleasesByName,
                releaseNamesOfEarlierContainers: releaseNamesOfEarlierContainers,
                templateHosterClassNames: templateHosterClassNames,
                releaseFolderBasePaths: releaseFolderBasePaths,
                archivePartFileNamesByFolderPath: archivePartFileNamesByFolderPath,
                releaseNamesByChosenArchiveFolderPath: releaseNamesByChosenArchiveFolderPath,
                folderPathsOfReplacedRelease: [],
                cancellationToken: cancellationToken
            );

            releaseNamesOfEarlierContainers.UnionWith(
                GetReleaseNames(container, mirrorsOfArchives)
            );

            var notImportableReason = previewResult.GetNotImportableReason();

            container.PreviewJson = previewResult.Preview.ToJson();
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

    public async Task<LinkCrypterImportLocalFileSearchResult> SearchLocalFilesAsync(
        LinkCrypterImport linkCrypterImport,
        CancellationToken cancellationToken
    )
    {
        var releaseTemplate = await GetReleaseTemplateAsync(linkCrypterImport, cancellationToken);

        return new LinkCrypterImportLocalFileSearchResult(
            ArchivePartFileNamesByFolderPath: await GetArchivePartFileNamesByFolderPathAsync(
                linkCrypterImport,
                cancellationToken
            ),
            ReleaseFolderBasePaths: await GetReleaseFolderBasePathsAsync(
                releaseTemplate,
                cancellationToken
            )
        );
    }

    public async Task<LinkCrypterImportContainerPreviewResult> CreatePreviewOfContainerAsync(
        LinkCrypterImportContainer container,
        IReadOnlyCollection<string> folderPathsOfReplacedRelease,
        LinkCrypterImportLocalFileSearchResult localFileSearchResult,
        Dictionary<string, string> releaseNamesByChosenArchiveFolderPath,
        CancellationToken cancellationToken
    )
    {
        var releaseTemplate = await GetReleaseTemplateAsync(
            container.LinkCrypterImport,
            cancellationToken
        );

        var mirrors = GetMirrorsWithoutDecisions(container);
        var mirrorsOfArchives = LinkCrypterImportMirrorSplitter.SplitByArchiveBaseName(mirrors);

        var existingReleasesByName = await GetExistingReleasesByNameAsync(
            GetReleaseNames(container, mirrorsOfArchives),
            cancellationToken
        );

        return await CreatePreviewAsync(
            container: container,
            mirrors: mirrors,
            mirrorsOfArchives: mirrorsOfArchives,
            releaseTemplate: releaseTemplate,
            existingReleasesByName: existingReleasesByName,
            releaseNamesOfEarlierContainers: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            templateHosterClassNames: GetTemplateHosterClassNames(releaseTemplate),
            releaseFolderBasePaths: localFileSearchResult.ReleaseFolderBasePaths,
            archivePartFileNamesByFolderPath: localFileSearchResult.ArchivePartFileNamesByFolderPath,
            releaseNamesByChosenArchiveFolderPath: releaseNamesByChosenArchiveFolderPath,
            folderPathsOfReplacedRelease: folderPathsOfReplacedRelease,
            cancellationToken: cancellationToken
        );
    }

    private async Task<ReleaseTemplate> GetReleaseTemplateAsync(
        LinkCrypterImport linkCrypterImport,
        CancellationToken cancellationToken
    )
    {
        return await releaseWriteRepository.GetTemplateForReleaseCreationOrDefaultAsync(
                linkCrypterImport.ReleaseTemplateId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Release template {linkCrypterImport.ReleaseTemplateId} of link crypter import {linkCrypterImport.Id} was not found."
            );
    }

    private static List<string> GetTemplateHosterClassNames(ReleaseTemplate releaseTemplate)
    {
        return releaseTemplate
            .UploadConfigTemplates.Where(template =>
                template.UploadSource is UploadSource.ArchiveFiles
            )
            .OrderBy(template => template.Id)
            .Select(template => template.GetHosterClassName())
            .ToList();
    }

    private async Task<List<string>> GetReleaseFolderBasePathsAsync(
        ReleaseTemplate releaseTemplate,
        CancellationToken cancellationToken
    )
    {
        return releaseTemplate.ReleaseType is ReleaseType.Managed
            ? await GetReleaseFolderBasePathsAsync(cancellationToken)
            : [];
    }

    private static List<LinkCrypterImportMirror> GetMirrorsWithoutDecisions(
        LinkCrypterImportContainer container
    )
    {
        return LinkCrypterImportContainerPreview
            .FromJson(container.PreviewJson)
            .Mirrors.Select(mirror => mirror with { Decision = null, UploadByBearcatReason = null })
            .ToList();
    }

    private static List<string> GetReleaseNames(
        LinkCrypterImportContainer container,
        IReadOnlyList<LinkCrypterImportMirrorsOfArchive> mirrorsOfArchives
    )
    {
        return mirrorsOfArchives.Count == 0
            ? [container.ReleaseName]
            : mirrorsOfArchives
                .Select(mirrorsOfArchive => mirrorsOfArchive.ArchiveBaseName)
                .ToList();
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

            using var proxyScope = RegistrationProxyScope.EnterForHosterUpload(registration);

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
        List<string> releaseNames,
        CancellationToken cancellationToken
    )
    {
        var releases = await repository.GetReleasesByNameAsync(
            releaseNames.Distinct().ToList(),
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

    private async Task<LinkCrypterImportContainerPreviewResult> CreatePreviewAsync(
        LinkCrypterImportContainer container,
        List<LinkCrypterImportMirror> mirrors,
        IReadOnlyList<LinkCrypterImportMirrorsOfArchive> mirrorsOfArchives,
        ReleaseTemplate releaseTemplate,
        Dictionary<string, Release> existingReleasesByName,
        HashSet<string> releaseNamesOfEarlierContainers,
        List<string> templateHosterClassNames,
        List<string> releaseFolderBasePaths,
        Dictionary<string, HashSet<string>> archivePartFileNamesByFolderPath,
        Dictionary<string, string> releaseNamesByChosenArchiveFolderPath,
        IReadOnlyCollection<string> folderPathsOfReplacedRelease,
        CancellationToken cancellationToken
    )
    {
        var isCollectionContainer = mirrorsOfArchives.Count > 0;
        var releaseNamesOfContainer = GetReleaseNames(container, mirrorsOfArchives);

        List<(
            string ReleaseName,
            IReadOnlyList<LinkCrypterImportMirror> Mirrors
        )> mirrorsPerRelease = isCollectionContainer
            ? mirrorsOfArchives
                .Select(mirrorsOfArchive =>
                    (mirrorsOfArchive.ArchiveBaseName, mirrorsOfArchive.Mirrors)
                )
                .ToList()
            : [(container.ReleaseName, mirrors)];

        var releasePreviews = new List<LinkCrypterImportReleasePreview>();

        foreach (var (releaseName, releaseMirrors) in mirrorsPerRelease)
        {
            var existingRelease = existingReleasesByName.GetValueOrDefault(releaseName);

            var releasePreview = await CreateReleasePreviewAsync(
                container: container,
                releaseName: releaseName,
                mirrors: releaseMirrors,
                existingRelease: existingRelease,
                isNameOfEarlierContainer: releaseNamesOfEarlierContainers.Contains(releaseName),
                templateHosterClassNames: templateHosterClassNames,
                releaseFolderBasePaths: releaseFolderBasePaths,
                archivePartFileNamesByFolderPath: archivePartFileNamesByFolderPath,
                releaseNamesByChosenArchiveFolderPath: releaseNamesByChosenArchiveFolderPath,
                folderPathsOfReplacedRelease: folderPathsOfReplacedRelease,
                isCollectionContainer: isCollectionContainer,
                releaseNamesOfContainer: releaseNamesOfContainer,
                cancellationToken: cancellationToken
            );

            releasePreviews.Add(releasePreview);
        }

        var (releaseCollectionName, containerNotImportableReason) = isCollectionContainer
            ? DetectReleaseCollection(releaseNamesOfContainer, releaseTemplate)
            : (null, null);

        return new LinkCrypterImportContainerPreviewResult(
            Preview: new LinkCrypterImportContainerPreview(
                Mirrors: mirrors,
                ReleaseCollectionName: releaseCollectionName,
                Releases: releasePreviews
            ),
            ContainerNotImportableReason: containerNotImportableReason
        );
    }

    private static (
        string? ReleaseCollectionName,
        LinkCrypterImportNotImportableReason? NotImportableReason
    ) DetectReleaseCollection(List<string> releaseNames, ReleaseTemplate releaseTemplate)
    {
        if (
            releaseTemplate.ReleaseCollectionDetectionMode
            is ReleaseCollectionDetectionMode.Disabled
        )
        {
            return (null, LinkCrypterImportNotImportableReason.ReleaseCollectionDetectionDisabled);
        }

        var detectionResults = releaseNames
            .Select(releaseName =>
                ReleaseCollectionDetectionService.Detect(releaseName, releaseTemplate)
            )
            .ToList();

        if (
            detectionResults.Any(detectionResult => detectionResult is null)
            || detectionResults
                .Select(detectionResult => detectionResult!.Key)
                .Distinct(StringComparer.Ordinal)
                .Count() > 1
        )
        {
            return (null, LinkCrypterImportNotImportableReason.ReleasesNotInSameReleaseCollection);
        }

        return (detectionResults.First()!.Name, null);
    }

    private async Task<LinkCrypterImportReleasePreview> CreateReleasePreviewAsync(
        LinkCrypterImportContainer container,
        string releaseName,
        IReadOnlyList<LinkCrypterImportMirror> mirrors,
        Release? existingRelease,
        bool isNameOfEarlierContainer,
        List<string> templateHosterClassNames,
        List<string> releaseFolderBasePaths,
        Dictionary<string, HashSet<string>> archivePartFileNamesByFolderPath,
        Dictionary<string, string> releaseNamesByChosenArchiveFolderPath,
        IReadOnlyCollection<string> folderPathsOfReplacedRelease,
        bool isCollectionContainer,
        List<string> releaseNamesOfContainer,
        CancellationToken cancellationToken
    )
    {
        var releaseAction = GetReleaseAction(existingRelease, isNameOfEarlierContainer);

        var decidedMirrors = DecideMirrors(
            mirrors: mirrors,
            hosterClassNames: existingRelease is null
                ? templateHosterClassNames
                : GetArchiveFilesHosterClassNames(existingRelease)
        );

        var releaseFolderPath = existingRelease is null
            ? await FindUnusedReleaseFolderPathAsync(
                releaseName,
                releaseFolderBasePaths,
                cancellationToken
            )
            : null;

        var (archiveFolderPath, archiveFolderContainsArchivesOfOtherReleases) = existingRelease
            is null
            ? await FindUnusedArchiveFolderPathAsync(
                container: container,
                releaseName: releaseName,
                mirrors: decidedMirrors,
                canUseFolderWithArchivesOfOtherReleases: isCollectionContainer
                    && releaseAction is LinkCrypterImportReleaseAction.CreateRelease
                    && releaseFolderPath is null,
                releaseNamesOfContainer: releaseNamesOfContainer,
                archivePartFileNamesByFolderPath: archivePartFileNamesByFolderPath,
                releaseNamesByChosenArchiveFolderPath: releaseNamesByChosenArchiveFolderPath,
                folderPathsOfReplacedRelease: folderPathsOfReplacedRelease,
                cancellationToken: cancellationToken
            )
            : (null, false);

        if (archiveFolderPath is not null)
        {
            releaseNamesByChosenArchiveFolderPath.TryAdd(
                archiveFolderPath,
                archiveFolderContainsArchivesOfOtherReleases ? container.ReleaseName : releaseName
            );
        }

        if (archiveFolderPath is not null && archiveFolderPath == releaseFolderPath)
        {
            releaseFolderPath = null;
        }

        var notImportableReason = GetNotImportableReason(
            mirrors: decidedMirrors,
            releaseAction: releaseAction,
            releaseFolderPath: releaseFolderPath,
            archiveFolderPath: archiveFolderPath,
            archiveFilesBasePath: container
                .LinkCrypterImport
                .ArchiveFilesBasePathForReleasesWithoutLocalFiles
        );

        return new LinkCrypterImportReleasePreview(
            ReleaseName: releaseName,
            Mirrors: decidedMirrors,
            ReleaseFolderPath: releaseFolderPath,
            ArchiveFolderPath: archiveFolderPath,
            ArchiveFolderContainsArchivesOfOtherReleases: archiveFolderContainsArchivesOfOtherReleases,
            ReleaseAction: releaseAction,
            ExistingReleaseId: existingRelease?.Id,
            NotImportableReason: notImportableReason
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

    private async Task<(
        string? ArchiveFolderPath,
        bool ContainsArchivesOfOtherReleases
    )> FindUnusedArchiveFolderPathAsync(
        LinkCrypterImportContainer container,
        string releaseName,
        List<LinkCrypterImportMirror> mirrors,
        bool canUseFolderWithArchivesOfOtherReleases,
        List<string> releaseNamesOfContainer,
        Dictionary<string, HashSet<string>> archivePartFileNamesByFolderPath,
        Dictionary<string, string> releaseNamesByChosenArchiveFolderPath,
        IReadOnlyCollection<string> folderPathsOfReplacedRelease,
        CancellationToken cancellationToken
    )
    {
        var firstAdoptedMirror = mirrors.FirstOrDefault(mirror =>
            mirror.Decision is LinkCrypterImportMirrorDecision.Adopt
        );

        if (firstAdoptedMirror is null)
        {
            return (null, false);
        }

        var fileNames = firstAdoptedMirror.Files.Select(file => file.FileName!).ToList();

        var folderPathWithSameArchiveParts = await FindUnusedFolderPathAsync(
            folderPaths: archivePartFileNamesByFolderPath
                .Where(entry => entry.Value.SetEquals(fileNames))
                .Select(entry => entry.Key)
                .ToList(),
            releaseName: releaseName,
            releaseNamesByChosenArchiveFolderPath: releaseNamesByChosenArchiveFolderPath,
            folderPathsOfReplacedRelease: folderPathsOfReplacedRelease,
            cancellationToken: cancellationToken
        );

        if (folderPathWithSameArchiveParts is not null || !canUseFolderWithArchivesOfOtherReleases)
        {
            return (folderPathWithSameArchiveParts, false);
        }

        var folderPathWithArchivePartsOfContainer = await FindUnusedFolderPathAsync(
            folderPaths: archivePartFileNamesByFolderPath
                .Where(entry =>
                    entry.Value.IsSupersetOf(fileNames)
                    && ContainsOnlyArchivesOfReleases(entry.Value, releaseNamesOfContainer)
                )
                .Select(entry => entry.Key)
                .ToList(),
            releaseName: container.ReleaseName,
            releaseNamesByChosenArchiveFolderPath: releaseNamesByChosenArchiveFolderPath,
            folderPathsOfReplacedRelease: folderPathsOfReplacedRelease,
            cancellationToken: cancellationToken
        );

        return (
            folderPathWithArchivePartsOfContainer,
            folderPathWithArchivePartsOfContainer is not null
        );
    }

    private static bool ContainsOnlyArchivesOfReleases(
        HashSet<string> archivePartFileNames,
        List<string> releaseNames
    )
    {
        return archivePartFileNames.All(fileName =>
            releaseNames.Contains(
                ArchivePartFileNames.GetArchiveBaseName(fileName)!,
                StringComparer.OrdinalIgnoreCase
            )
        );
    }

    private async Task<string?> FindUnusedFolderPathAsync(
        List<string> folderPaths,
        string releaseName,
        Dictionary<string, string> releaseNamesByChosenArchiveFolderPath,
        IReadOnlyCollection<string> folderPathsOfReplacedRelease,
        CancellationToken cancellationToken
    )
    {
        var folderPathsNotChosenForOtherRelease = folderPaths
            .Where(folderPath =>
                !IsArchiveFolderChosenForOtherRelease(
                    folderPath: folderPath,
                    releaseName: releaseName,
                    releaseNamesByChosenArchiveFolderPath: releaseNamesByChosenArchiveFolderPath
                )
            )
            .Order(StringComparer.Ordinal)
            .ToList();

        if (folderPathsNotChosenForOtherRelease.Count == 0)
        {
            return null;
        }

        var usedFolderPaths =
            await releaseFolderUsageRepository.GetUsedReleaseArchiveOrRemoteDownloadFolderPathsAsync(
                folderPathsNotChosenForOtherRelease,
                cancellationToken
            );

        usedFolderPaths.ExceptWith(folderPathsOfReplacedRelease);

        return folderPathsNotChosenForOtherRelease.FirstOrDefault(folderPath =>
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
