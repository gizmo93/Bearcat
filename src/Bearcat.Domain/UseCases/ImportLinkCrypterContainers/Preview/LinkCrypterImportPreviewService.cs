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

        if (await CheckFilesAsync(linkCrypterImport, containers, cancellationToken))
        {
            await CreatePreviewsAsync(linkCrypterImport, containers, cancellationToken);
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

    private async Task CreatePreviewsAsync(
        LinkCrypterImport linkCrypterImport,
        List<LinkCrypterImportContainer> containers,
        CancellationToken cancellationToken
    )
    {
        var releaseTemplate =
            await releaseWriteRepository.GetTemplateForReleaseCreationOrDefaultAsync(
                linkCrypterImport.ReleaseTemplateId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Release template {linkCrypterImport.ReleaseTemplateId} of link crypter import {linkCrypterImport.Id} was not found."
            );

        var uploadConfigTemplates = releaseTemplate
            .UploadConfigTemplates.Where(template =>
                template.UploadSource is UploadSource.ArchiveFiles
            )
            .OrderBy(template => template.Id)
            .ToList();

        var releaseFolderBasePaths =
            releaseTemplate.ReleaseType is ReleaseType.Managed
                ? await GetReleaseFolderBasePathsAsync(cancellationToken)
                : [];

        var existingReleaseNames = await repository.GetExistingReleaseNamesAsync(
            containers.Select(container => container.ContainerName.Trim()).ToList(),
            cancellationToken
        );

        var releaseNamesOfEarlierContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        linkCrypterImport.StartProgress(containers.Count);
        await repository.SaveChangesAsync(cancellationToken);

        foreach (var container in containers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var releaseName = container.ContainerName.Trim();

            var notImportableReason =
                GetNotImportableReasonOfReleaseName(
                    releaseName: releaseName,
                    existingReleaseNames: existingReleaseNames,
                    releaseNamesOfEarlierContainers: releaseNamesOfEarlierContainers
                )
                ?? await CreatePreviewAsync(
                    container: container,
                    releaseName: releaseName,
                    uploadConfigTemplates: uploadConfigTemplates,
                    releaseFolderBasePaths: releaseFolderBasePaths,
                    cancellationToken: cancellationToken
                );

            releaseNamesOfEarlierContainers.Add(releaseName);
            container.NotImportableReason = notImportableReason;

            container.Status = notImportableReason is null
                ? LinkCrypterImportContainerStatus.Importable
                : LinkCrypterImportContainerStatus.NotImportable;

            container.IsSelected = notImportableReason is null;
            linkCrypterImport.ProcessedContainerCount++;

            await repository.SaveChangesAsync(cancellationToken);
        }

        var importableContainerCount = containers.Count(container =>
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

    private static LinkCrypterImportNotImportableReason? GetNotImportableReasonOfReleaseName(
        string releaseName,
        HashSet<string> existingReleaseNames,
        HashSet<string> releaseNamesOfEarlierContainers
    )
    {
        if (existingReleaseNames.Contains(releaseName))
        {
            return LinkCrypterImportNotImportableReason.ReleaseAlreadyExists;
        }

        if (releaseNamesOfEarlierContainers.Contains(releaseName))
        {
            return LinkCrypterImportNotImportableReason.SameNameAsEarlierContainer;
        }

        return null;
    }

    private async Task<LinkCrypterImportNotImportableReason?> CreatePreviewAsync(
        LinkCrypterImportContainer container,
        string releaseName,
        List<UploadConfigTemplate> uploadConfigTemplates,
        List<string> releaseFolderBasePaths,
        CancellationToken cancellationToken
    )
    {
        var mirrors = DecideMirrors(
            LinkCrypterImportContainerPreview.FromJson(container.PreviewJson).Mirrors,
            uploadConfigTemplates
        );

        var releaseFolderPath = await FindUnusedReleaseFolderPathAsync(
            releaseName,
            releaseFolderBasePaths,
            cancellationToken
        );

        container.PreviewJson = new LinkCrypterImportContainerPreview(
            Mirrors: mirrors,
            ReleaseFolderPath: releaseFolderPath
        ).ToJson();

        return GetNotImportableReasonOfPreview(
            mirrors: mirrors,
            releaseFolderPath: releaseFolderPath,
            archiveFilesBasePath: container
                .LinkCrypterImport
                .ArchiveFilesBasePathForReleasesWithoutLocalFiles
        );
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

    private static List<LinkCrypterImportMirror> DecideMirrors(
        IReadOnlyList<LinkCrypterImportMirror> mirrors,
        List<UploadConfigTemplate> uploadConfigTemplates
    )
    {
        var usedUploadConfigTemplateIds = new HashSet<int>();
        var decidedMirrors = new List<LinkCrypterImportMirror>();

        foreach (var mirror in mirrors.OrderBy(mirror => mirror.MirrorNumber))
        {
            if (mirror.HosterClassName is null)
            {
                decidedMirrors.Add(
                    mirror with
                    {
                        Decision = LinkCrypterImportMirrorDecision.UnsupportedHoster,
                    }
                );
                continue;
            }

            var uploadConfigTemplate = uploadConfigTemplates.FirstOrDefault(template =>
                template.GetHosterClassName() == mirror.HosterClassName
                && !usedUploadConfigTemplateIds.Contains(template.Id)
            );

            if (uploadConfigTemplate is null)
            {
                decidedMirrors.Add(
                    mirror with
                    {
                        Decision = LinkCrypterImportMirrorDecision.NotInReleaseTemplate,
                    }
                );
                continue;
            }

            usedUploadConfigTemplateIds.Add(uploadConfigTemplate.Id);

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
                    UploadConfigTemplateId = uploadConfigTemplate.Id,
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

    private static LinkCrypterImportNotImportableReason? GetNotImportableReasonOfPreview(
        List<LinkCrypterImportMirror> mirrors,
        string? releaseFolderPath,
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
            return LinkCrypterImportNotImportableReason.NoMirrorForReleaseTemplateHoster;
        }

        if (releaseFolderPath is not null)
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
