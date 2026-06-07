using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadConfigLinkCrypterReadRepository(
    IBearcatReadDbContext dbRead,
    ILinkCrypterFactory linkCrypterFactory
) : IUploadConfigLinkCrypterReadRepository
{
    public async Task<UploadConfigLinkCrypterReadModel> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var linkCryptersByClassName = linkCrypterFactory
            .GetLinkCrypters()
            .ToDictionary(l => l.ClassName);

        var item = await dbRead
            .UploadConfigLinkCrypters.Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.LinkCrypterRegistration.LinkCrypterClassName,
                LinkCrypterRegistrationName = u.LinkCrypterRegistration.Name,
                u.LinkCrypterRegistrationId,
                u.Password,
                u.ContainerScope,
                LinkCrypterIsActive = u.LinkCrypterRegistration.IsActive,
                u.EnableCaptcha,
                u.EnableContainerDownload,
                u.EnableClickAndLoad,
                ReleaseCollectionId =
                    u.UploadConfig.CollectionUploadSlot == null
                        ? null
                        : (int?)u.UploadConfig.CollectionUploadSlot.ReleaseCollectionId,
            })
            .FirstAsync(cancellationToken: cancellationToken);

        var linkCrypter = linkCryptersByClassName[item.LinkCrypterClassName];

        return new UploadConfigLinkCrypterReadModel(
            item.Id,
            linkCrypter.Name,
            item.LinkCrypterRegistrationName,
            item.LinkCrypterRegistrationId,
            item.Password,
            item.ContainerScope,
            item.LinkCrypterIsActive,
            item.EnableCaptcha,
            item.EnableContainerDownload,
            item.EnableClickAndLoad,
            linkCrypter.SupportsCaptcha,
            linkCrypter.SupportsContainerDownload,
            linkCrypter.SupportsClickAndLoad,
            item.ReleaseCollectionId
        );
    }

    public async Task<IReadOnlyList<UploadConfigLinkCrypterReadModel>> GetByUploadConfigIdAsync(
        int uploadConfigId,
        CancellationToken cancellationToken = default
    )
    {
        var linkCryptersByClassName = linkCrypterFactory
            .GetLinkCrypters()
            .ToDictionary(l => l.ClassName);

        var items = await dbRead
            .UploadConfigLinkCrypters.Where(u => u.UploadConfigId == uploadConfigId)
            .Select(u => new
            {
                u.Id,
                u.LinkCrypterRegistration.LinkCrypterClassName,
                LinkCrypterRegistrationName = u.LinkCrypterRegistration.Name,
                u.LinkCrypterRegistrationId,
                u.Password,
                u.ContainerScope,
                LinkCrypterIsActive = u.LinkCrypterRegistration.IsActive,
                u.EnableCaptcha,
                u.EnableContainerDownload,
                u.EnableClickAndLoad,
                ReleaseCollectionId =
                    u.UploadConfig.CollectionUploadSlot == null
                        ? null
                        : (int?)u.UploadConfig.CollectionUploadSlot.ReleaseCollectionId,
            })
            .ToListAsync(cancellationToken: cancellationToken);

        return items
            .Select(item =>
            {
                var linkCrypter = linkCryptersByClassName[item.LinkCrypterClassName];

                return new UploadConfigLinkCrypterReadModel(
                    item.Id,
                    linkCrypter.Name,
                    item.LinkCrypterRegistrationName,
                    item.LinkCrypterRegistrationId,
                    item.Password,
                    item.ContainerScope,
                    item.LinkCrypterIsActive,
                    item.EnableCaptcha,
                    item.EnableContainerDownload,
                    item.EnableClickAndLoad,
                    linkCrypter.SupportsCaptcha,
                    linkCrypter.SupportsContainerDownload,
                    linkCrypter.SupportsClickAndLoad,
                    item.ReleaseCollectionId
                );
            })
            .ToList();
    }

    public async Task<IReadOnlyList<LinkCrypterOptionReadModel>> GetLinkCrypterOptionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var linkCryptersByClassName = linkCrypterFactory
            .GetLinkCrypters()
            .ToDictionary(l => l.ClassName);

        var registrations = await dbRead
            .LinkCrypterRegistrations.Where(l => l.IsActive)
            .Select(l => new
            {
                l.Id,
                l.Name,
                l.LinkCrypterClassName,
            })
            .ToListAsync(cancellationToken);

        return registrations
            .Select(registration =>
            {
                var linkCrypter = linkCryptersByClassName[registration.LinkCrypterClassName];

                return new LinkCrypterOptionReadModel(
                    registration.Id,
                    registration.Name,
                    linkCrypter.SupportsCaptcha,
                    linkCrypter.SupportsContainerDownload,
                    linkCrypter.SupportsClickAndLoad
                );
            })
            .ToList();
    }
}
