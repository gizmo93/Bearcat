using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.MultiUploadContainers;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageLinkCrypterContainers.ReleaseAllHosters;

public class ReleaseAllHostersLinkCrypterContainerService(
    ILinkCrypterContainerCreationWriteRepository repository,
    ILogger<ReleaseAllHostersLinkCrypterContainerService> logger,
    TimeProvider timeProvider,
    MultiUploadLinkCrypterContainerService multiUploadContainerService
)
{
    private const string ContainerDescriptionInMessages = "All hosters link crypter container";

    private const string DifferentSettingsErrorMessage =
        "Link crypter settings differ across upload configs.";

    public async Task UpdateOutdatedContainersAsync(CancellationToken cancellationToken)
    {
        var releaseIds = await repository.GetReleaseIdsWithOutdatedAllHostersContainersAsync(
            updateFailedBefore: timeProvider
                .GetLocalNow()
                .AddMinutes(
                    -MultiUploadLinkCrypterContainerService.UpdateFailedRetryDelayInMinutes
                ),
            cancellationToken: cancellationToken
        );

        foreach (var releaseId in releaseIds)
        {
            logger.LogInformation(
                "Updating all hosters link crypter containers of release {ReleaseId}",
                releaseId
            );
            await UpdateContainersAsync(releaseId, cancellationToken);
        }
    }

    private async Task UpdateContainersAsync(int releaseId, CancellationToken cancellationToken)
    {
        var release = await repository.GetReleaseWithUploadConfigsAsync(
            releaseId,
            cancellationToken
        );
        var existingContainers = await repository.GetAllHostersContainersAsync(
            releaseId,
            cancellationToken
        );
        var memberLinkCryptersByRegistrationId = GetMemberLinkCryptersByRegistrationId(release);

        await multiUploadContainerService.RemoveContainersAsync(
            existingContainers
                .Where(container =>
                    !memberLinkCryptersByRegistrationId.ContainsKey(
                        container.LinkCrypterRegistrationId
                    )
                )
                .ToList(),
            cancellationToken
        );

        foreach (var (registrationId, memberLinkCrypters) in memberLinkCryptersByRegistrationId)
        {
            if (!memberLinkCrypters[0].LinkCrypterRegistration.IsActive)
            {
                continue;
            }

            await CreateOrUpdateContainerAsync(
                release: release,
                existingContainer: existingContainers.FirstOrDefault(container =>
                    container.LinkCrypterRegistrationId == registrationId
                ),
                memberLinkCrypters: memberLinkCrypters,
                ignoreUpdateRetryDelay: false,
                cancellationToken: cancellationToken
            );
        }
    }

    public async Task<LinkCrypterContainer> ForceUpdateContainerAsync(
        int releaseId,
        int containerId,
        CancellationToken cancellationToken
    )
    {
        var release = await repository.GetReleaseWithUploadConfigsAsync(
            releaseId,
            cancellationToken
        );
        var container = await GetAllHostersContainerAsync(
            releaseId: releaseId,
            containerId: containerId,
            cancellationToken: cancellationToken
        );

        await CreateOrUpdateContainerAsync(
            release: release,
            existingContainer: container,
            memberLinkCrypters: GetMemberLinkCryptersByRegistrationId(release)[
                container.LinkCrypterRegistrationId
            ],
            ignoreUpdateRetryDelay: true,
            cancellationToken: cancellationToken
        );

        return container;
    }

    public async Task<LinkCrypterContainer> RecreateContainerAsync(
        int releaseId,
        int containerId,
        CancellationToken cancellationToken
    )
    {
        var release = await repository.GetReleaseWithUploadConfigsAsync(
            releaseId,
            cancellationToken
        );
        var container = await GetAllHostersContainerAsync(
            releaseId: releaseId,
            containerId: containerId,
            cancellationToken: cancellationToken
        );
        var memberLinkCrypters = GetMemberLinkCryptersByRegistrationId(release)[
            container.LinkCrypterRegistrationId
        ];
        var definition = CreateDefinition(release, memberLinkCrypters);

        if (definition.Uploads.Count == 0)
        {
            return container;
        }

        if (!HaveSameContainerSettings(memberLinkCrypters))
        {
            await MarkContainerAsInvalidAsync(definition, container, cancellationToken);
            return container;
        }

        await multiUploadContainerService.RecreateContainerAsync(
            definition,
            container,
            cancellationToken
        );
        return container;
    }

    private async Task<LinkCrypterContainer> GetAllHostersContainerAsync(
        int releaseId,
        int containerId,
        CancellationToken cancellationToken
    )
    {
        var containers = await repository.GetAllHostersContainersAsync(
            releaseId,
            cancellationToken
        );

        return containers.Single(container => container.Id == containerId);
    }

    private async Task CreateOrUpdateContainerAsync(
        Release release,
        LinkCrypterContainer? existingContainer,
        IReadOnlyList<UploadConfigLinkCrypter> memberLinkCrypters,
        bool ignoreUpdateRetryDelay,
        CancellationToken cancellationToken
    )
    {
        var definition = CreateDefinition(release, memberLinkCrypters);

        if (definition.Uploads.Count == 0)
        {
            return;
        }

        if (!HaveSameContainerSettings(memberLinkCrypters))
        {
            logger.LogWarning(
                "Skipping all hosters link crypter container for release {ReleaseId}, registration {RegistrationId}: {Reason}",
                release.Id,
                definition.LinkCrypterConfig.LinkCrypterRegistrationId,
                DifferentSettingsErrorMessage
            );

            if (existingContainer is null)
            {
                await multiUploadContainerService.AddInvalidContainerAsync(
                    definition: definition,
                    container: CreateContainer(release, definition.LinkCrypterConfig),
                    errorMessage: DifferentSettingsErrorMessage,
                    cancellationToken: cancellationToken
                );
                return;
            }

            await MarkContainerAsInvalidAsync(definition, existingContainer, cancellationToken);
            return;
        }

        if (existingContainer is null)
        {
            await multiUploadContainerService.AddContainerAsync(
                definition,
                CreateContainer(release, definition.LinkCrypterConfig),
                cancellationToken
            );
            return;
        }

        await multiUploadContainerService.UpdateContainerAsync(
            definition: definition,
            container: existingContainer,
            ignoreUpdateRetryDelay: ignoreUpdateRetryDelay,
            cancellationToken: cancellationToken
        );
    }

    private async Task MarkContainerAsInvalidAsync(
        MultiUploadLinkCrypterContainerDefinition definition,
        LinkCrypterContainer container,
        CancellationToken cancellationToken
    )
    {
        if (!container.HasContainerUrl())
        {
            container.ReplaceSourceUploads(definition.Uploads);
        }

        await multiUploadContainerService.MarkContainerAsInvalidAsync(
            definition: definition,
            container: container,
            errorMessage: DifferentSettingsErrorMessage,
            cancellationToken: cancellationToken
        );
    }

    private static LinkCrypterContainer CreateContainer(
        Release release,
        UploadConfigLinkCrypter linkCrypterConfig
    )
    {
        return new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseAllHosters,
            ReleaseId = release.Id,
            LinkCrypterRegistrationId = linkCrypterConfig.LinkCrypterRegistrationId,
        };
    }

    private static MultiUploadLinkCrypterContainerDefinition CreateDefinition(
        Release release,
        IReadOnlyList<UploadConfigLinkCrypter> memberLinkCrypters
    )
    {
        var uploads = GetLatestCompletedUploadPerUploadConfig(memberLinkCrypters);

        return new MultiUploadLinkCrypterContainerDefinition(
            ContainerDescriptionInMessages: ContainerDescriptionInMessages,
            OwnerDescriptionInMessages: $"release {release.Name}",
            ContainerName: release.Name,
            InvalidNotificationKind: NotificationKind.ReleaseAllHostersLinkCrypterContainerInvalid,
            ResetUploadsPostedAt: () => release.UploadsPostedAt = null,
            LinkCrypterConfig: memberLinkCrypters[0],
            Uploads: uploads,
            LinksPerMirrorNumber: uploads
                .Index()
                .ToDictionary(entry => entry.Index + 1, entry => GetUploadLinks(entry.Item))
        );
    }

    private static Dictionary<
        int,
        List<UploadConfigLinkCrypter>
    > GetMemberLinkCryptersByRegistrationId(Release release)
    {
        return release
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .Where(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseAllHosters
            )
            .GroupBy(linkCrypter => linkCrypter.LinkCrypterRegistrationId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(linkCrypter => linkCrypter.Id).ToList()
            );
    }

    private static bool HaveSameContainerSettings(
        IReadOnlyList<UploadConfigLinkCrypter> memberLinkCrypters
    )
    {
        return memberLinkCrypters.All(linkCrypter =>
            linkCrypter.HasSameContainerSettingsAs(memberLinkCrypters[0])
        );
    }

    private static List<Upload> GetLatestCompletedUploadPerUploadConfig(
        IReadOnlyList<UploadConfigLinkCrypter> memberLinkCrypters
    )
    {
        return memberLinkCrypters
            .Select(linkCrypter => linkCrypter.UploadConfig)
            .DistinctBy(uploadConfig => uploadConfig.Id)
            .OrderBy(uploadConfig => uploadConfig.Id)
            .Select(uploadConfig =>
                uploadConfig
                    .Uploads.Where(upload =>
                        upload.UploadState == UploadState.Completed
                        && upload.UploadedFiles.Count > 0
                    )
                    .MaxBy(upload => upload.Id)
            )
            .OfType<Upload>()
            .ToList();
    }

    private static IReadOnlyList<string> GetUploadLinks(Upload upload)
    {
        return upload
            .UploadedFiles.Select(file => file.HosterFileLink)
            .OrderBy(link => link)
            .ToList();
    }
}
