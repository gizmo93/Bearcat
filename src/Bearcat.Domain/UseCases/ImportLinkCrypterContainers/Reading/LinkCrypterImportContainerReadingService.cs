using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reading;

public class LinkCrypterImportContainerReadingService(
    ILinkCrypterImportRepository repository,
    IReleaseWriteRepository releaseWriteRepository,
    ILinkCrypterFactory linkCrypterFactory,
    IHosterFactory hosterFactory,
    ISecretProtector secretProtector,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<LinkCrypterImportContainerReadingService> logger
)
{
    public async Task ReadContainersAsync(
        LinkCrypterImport linkCrypterImport,
        CancellationToken cancellationToken
    )
    {
        var registration = linkCrypterImport.LinkCrypterRegistration;
        var linkCrypter = (ILinkCrypterWithContainerReading)
            linkCrypterFactory.Get(registration.LinkCrypterClassName);
        var linkCrypterConfig = linkCrypter.DeserializeConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        if (linkCrypterImport.Containers.Count == 0)
        {
            var listResult = await ListContainersAsync(
                linkCrypter: linkCrypter,
                linkCrypterConfig: linkCrypterConfig,
                registration: registration,
                cancellationToken: cancellationToken
            );

            if (!listResult.IsSuccess)
            {
                await MarkAsFailedAsync(linkCrypterImport, listResult, cancellationToken);
                return;
            }

            await AddListedContainersAsync(
                linkCrypterImport: linkCrypterImport,
                listedContainers: listResult.Containers,
                cancellationToken: cancellationToken
            );
        }

        var containersToRead = linkCrypterImport
            .Containers.Where(container =>
                container.Status is LinkCrypterImportContainerStatus.WaitingForLinks
            )
            .OrderBy(container => container.Id)
            .ToList();
        linkCrypterImport.StartProgress(containersToRead.Count);
        await repository.SaveChangesAsync(cancellationToken);

        var hosterClassNamesByHoster = GetHosterClassNamesByHoster();

        foreach (var container in containersToRead)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ReadContainerLinksAsync(
                container: container,
                linkCrypter: linkCrypter,
                linkCrypterConfig: linkCrypterConfig,
                registration: registration,
                hosterClassNamesByHoster: hosterClassNamesByHoster,
                cancellationToken: cancellationToken
            );

            linkCrypterImport.ProcessedContainerCount++;
            await repository.SaveChangesAsync(cancellationToken);
        }

        await AddHosterRegistrationChoicesAsync(linkCrypterImport, cancellationToken);

        linkCrypterImport.ChangeState(
            linkCrypterImport.HosterRegistrations.Count == 0
                ? LinkCrypterImportState.CheckingFiles
                : LinkCrypterImportState.WaitingForHosterRegistrations,
            timeProvider.GetLocalNow()
        );
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Read {ContainerCount} containers of link crypter import {LinkCrypterImportId}",
            linkCrypterImport.Containers.Count,
            linkCrypterImport.Id
        );
    }

    private static async Task<ListContainersResult> ListContainersAsync(
        ILinkCrypterWithContainerReading linkCrypter,
        ILinkCrypterConfig linkCrypterConfig,
        LinkCrypterRegistration registration,
        CancellationToken cancellationToken
    )
    {
        using var proxyScope = LinkCrypterRegistrationProxyScope.Enter(registration);

        return await linkCrypter.ListContainersAsync(linkCrypterConfig, cancellationToken);
    }

    private async Task MarkAsFailedAsync(
        LinkCrypterImport linkCrypterImport,
        ListContainersResult listResult,
        CancellationToken cancellationToken
    )
    {
        linkCrypterImport.MarkAsFailed(
            listResult.ErrorMessages.Count == 0
                ? ["The link crypter did not return the list of containers."]
                : listResult.ErrorMessages,
            timeProvider.GetLocalNow()
        );
        notificationService.Create(
            kind: NotificationKind.LinkCrypterImportFailed,
            message: $"Link crypter import {linkCrypterImport.Id} could not read the containers: {string.Join("; ", linkCrypterImport.ErrorMessages)}"
        );
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task AddListedContainersAsync(
        LinkCrypterImport linkCrypterImport,
        IReadOnlyList<ListedContainer> listedContainers,
        CancellationToken cancellationToken
    )
    {
        var existingContainerUrls = await repository.GetExistingLinkCrypterContainerUrlsAsync(
            listedContainers.Select(container => container.ContainerUrl).ToList(),
            cancellationToken
        );
        var emptyPreviewJson = new LinkCrypterImportContainerPreview(
            Mirrors: [],
            ReleaseFolderPath: null
        ).ToJson();

        foreach (var listedContainer in listedContainers)
        {
            linkCrypterImport.Containers.Add(
                new LinkCrypterImportContainer
                {
                    ContainerId = listedContainer.ContainerId,
                    ContainerUrl = listedContainer.ContainerUrl,
                    ContainerName = listedContainer.ContainerName,
                    ExternalReference = listedContainer.ExternalReference,
                    Status = existingContainerUrls.Contains(listedContainer.ContainerUrl)
                        ? LinkCrypterImportContainerStatus.AlreadyInBearcat
                        : LinkCrypterImportContainerStatus.WaitingForLinks,
                    IsSelected = false,
                    ErrorMessages = [],
                    PreviewJson = emptyPreviewJson,
                }
            );
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task ReadContainerLinksAsync(
        LinkCrypterImportContainer container,
        ILinkCrypterWithContainerReading linkCrypter,
        ILinkCrypterConfig linkCrypterConfig,
        LinkCrypterRegistration registration,
        Dictionary<IHoster, string> hosterClassNamesByHoster,
        CancellationToken cancellationToken
    )
    {
        ReadContainerLinksResult result;

        using (LinkCrypterRegistrationProxyScope.Enter(registration))
        {
            result = await linkCrypter.ReadContainerLinksAsync(
                linkCrypterConfig: linkCrypterConfig,
                containerUrl: container.ContainerUrl,
                externalReference: container.ExternalReference,
                cancellationToken: cancellationToken
            );
        }

        if (!result.IsSuccess)
        {
            container.Status = LinkCrypterImportContainerStatus.ReadingLinksFailed;
            container.ErrorMessages = result.ErrorMessages.ToList();
            logger.LogWarning(
                "Could not read the links of container {ContainerUrl}: {Errors}",
                container.ContainerUrl,
                string.Join("; ", result.ErrorMessages)
            );
            return;
        }

        var mirrors = result
            .LinksPerMirrorNumber.Where(entry => entry.Value.Count > 0)
            .OrderBy(entry => entry.Key)
            .Select(entry =>
                CreateMirror(
                    mirrorNumber: entry.Key,
                    links: entry.Value,
                    hosterClassNamesByHoster: hosterClassNamesByHoster
                )
            )
            .ToList();

        container.HasBackupLinks = result.HasBackupLinks;
        container.Status = LinkCrypterImportContainerStatus.WaitingForFileCheck;
        container.PreviewJson = new LinkCrypterImportContainerPreview(
            Mirrors: mirrors,
            ReleaseFolderPath: null
        ).ToJson();
    }

    private LinkCrypterImportMirror CreateMirror(
        int mirrorNumber,
        IReadOnlyList<string> links,
        Dictionary<IHoster, string> hosterClassNamesByHoster
    )
    {
        var hostersOfLinks = links.Select(hosterFactory.FindHosterByFileLink).ToList();
        var hoster = hostersOfLinks.All(hosterOfLink =>
            hosterOfLink is not null && ReferenceEquals(hosterOfLink, hostersOfLinks[0])
        )
            ? hostersOfLinks[0]
            : null;
        var hosterWithFileInfo = hoster as IHosterWithFileInfo;

        return new LinkCrypterImportMirror(
            MirrorNumber: mirrorNumber,
            HosterClassName: hoster is null ? null : hosterClassNamesByHoster[hoster],
            Files: links
                .Select(link => new LinkCrypterImportMirrorFile(
                    Link: link,
                    OwnFormatLink: hosterWithFileInfo?.ConvertToOwnFileLinkFormat(link),
                    IsOnline: null,
                    FileName: null,
                    SizeBytes: null
                ))
                .ToList(),
            Decision: null,
            UploadByBearcatReason: null,
            UploadConfigTemplateId: null
        );
    }

    private Dictionary<IHoster, string> GetHosterClassNamesByHoster()
    {
        var hosterClassNamesByHoster = new Dictionary<IHoster, string>(
            ReferenceEqualityComparer.Instance
        );

        foreach (var (hosterClassName, hoster) in hosterFactory.GetHostersByName())
        {
            hosterClassNamesByHoster[hoster] = hosterClassName;
        }

        return hosterClassNamesByHoster;
    }

    private async Task AddHosterRegistrationChoicesAsync(
        LinkCrypterImport linkCrypterImport,
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
        var archiveUploadConfigTemplates = releaseTemplate
            .UploadConfigTemplates.Where(template =>
                template.UploadSource is UploadSource.ArchiveFiles
            )
            .OrderBy(template => template.Id)
            .ToList();
        var templateHosterClassNames = archiveUploadConfigTemplates
            .Select(template => template.GetHosterClassName())
            .ToHashSet(StringComparer.Ordinal);
        var hostersByClassName = hosterFactory.GetHostersByName();

        var hosterClassNamesToChoose = linkCrypterImport
            .Containers.Where(container =>
                container.Status is LinkCrypterImportContainerStatus.WaitingForFileCheck
            )
            .SelectMany(container =>
                LinkCrypterImportContainerPreview.FromJson(container.PreviewJson).Mirrors
            )
            .Select(mirror => mirror.HosterClassName)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Where(hosterClassName =>
                templateHosterClassNames.Contains(hosterClassName)
                && hostersByClassName[hosterClassName] is IHosterWithFileInfo
            )
            .Order(StringComparer.Ordinal)
            .ToList();

        foreach (var hosterClassName in hosterClassNamesToChoose)
        {
            linkCrypterImport.HosterRegistrations.Add(
                new LinkCrypterImportHosterRegistration
                {
                    HosterClassName = hosterClassName,
                    HosterRegistrationId = archiveUploadConfigTemplates
                        .FirstOrDefault(template =>
                            template.HosterAccountPoolId is null
                            && template.GetHosterClassName() == hosterClassName
                        )
                        ?.HosterRegistrationId,
                }
            );
        }
    }
}
