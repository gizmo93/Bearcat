using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public static class CollectionLinkCrypterSync
{
    public static IReadOnlyDictionary<
        int,
        CollectionUploadSlotLinkCrypterSettings
    > GetSettingsFromSlot(CollectionUploadSlot slot)
    {
        return slot
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .Where(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            )
            .GroupBy(linkCrypter => linkCrypter.LinkCrypterRegistrationId)
            .ToDictionary(
                group => group.Key,
                group => ToSettings(group.OrderBy(item => item.Id).First())
            );
    }

    public static IReadOnlyDictionary<
        int,
        CollectionUploadSlotLinkCrypterSettings
    > NormalizeSettings(IReadOnlyList<CollectionUploadSlotLinkCrypterSettings> settings)
    {
        return settings
            .GroupBy(item => item.LinkCrypterRegistrationId)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    public static void ApplyToNewUploadConfig(
        UploadConfig uploadConfig,
        IReadOnlyDictionary<int, CollectionUploadSlotLinkCrypterSettings> settingsByRegistrationId
    )
    {
        Apply(
            uploadConfig,
            settingsByRegistrationId,
            removeLinkCrypter: linkCrypter => uploadConfig.LinkCrypters.Remove(linkCrypter)
        );
    }

    public static void ApplyToExistingUploadConfig(
        IReleaseCollectionWriteRepository writeRepository,
        UploadConfig uploadConfig,
        IReadOnlyDictionary<int, CollectionUploadSlotLinkCrypterSettings> settingsByRegistrationId
    )
    {
        Apply(
            uploadConfig,
            settingsByRegistrationId,
            removeLinkCrypter: linkCrypter =>
            {
                uploadConfig.LinkCrypters.Remove(linkCrypter);
                writeRepository.Remove(linkCrypter);
            }
        );
    }

    private static void Apply(
        UploadConfig uploadConfig,
        IReadOnlyDictionary<int, CollectionUploadSlotLinkCrypterSettings> settingsByRegistrationId,
        Action<UploadConfigLinkCrypter> removeLinkCrypter
    )
    {
        var sharedLinkCrypters = uploadConfig
            .LinkCrypters.Where(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            )
            .ToList();

        foreach (
            var linkCrypter in sharedLinkCrypters.Where(linkCrypter =>
                !settingsByRegistrationId.ContainsKey(linkCrypter.LinkCrypterRegistrationId)
            )
        )
        {
            removeLinkCrypter(linkCrypter);
        }

        var existingByRegistrationId = uploadConfig
            .LinkCrypters.Where(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            )
            .ToDictionary(linkCrypter => linkCrypter.LinkCrypterRegistrationId);

        foreach (var settings in settingsByRegistrationId.Values)
        {
            if (
                !existingByRegistrationId.TryGetValue(
                    settings.LinkCrypterRegistrationId,
                    out var linkCrypter
                )
            )
            {
                linkCrypter = new UploadConfigLinkCrypter
                {
                    LinkCrypterRegistrationId = settings.LinkCrypterRegistrationId,
                    ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
                    LinkCrypterContainers = [],
                };
                uploadConfig.LinkCrypters.Add(linkCrypter);
            }

            linkCrypter.Password = CleanOptional(settings.Password);
            linkCrypter.EnableCaptcha = settings.EnableCaptcha;
            linkCrypter.EnableContainerDownload = settings.EnableContainerDownload;
            linkCrypter.EnableClickAndLoad = settings.EnableClickAndLoad;
        }
    }

    private static CollectionUploadSlotLinkCrypterSettings ToSettings(
        UploadConfigLinkCrypter linkCrypter
    )
    {
        return new CollectionUploadSlotLinkCrypterSettings(
            linkCrypter.LinkCrypterRegistrationId,
            linkCrypter.Password,
            linkCrypter.EnableCaptcha,
            linkCrypter.EnableContainerDownload,
            linkCrypter.EnableClickAndLoad
        );
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
