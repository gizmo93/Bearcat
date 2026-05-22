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
        var linkCrypterNamesByClassName = linkCrypterFactory
            .GetLinkCrypters()
            .ToDictionary(l => l.ClassName, l => l.Name);

        return await dbRead
            .UploadConfigLinkCrypters.Where(u => u.Id == id)
            .Select(u => new UploadConfigLinkCrypterReadModel(
                u.Id,
                linkCrypterNamesByClassName[u.LinkCrypterRegistration.LinkCrypterClassName],
                u.LinkCrypterRegistration.Name,
                u.LinkCrypterRegistrationId,
                u.Password,
                u.LinkCrypterRegistration.IsActive
            ))
            .FirstAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<UploadConfigLinkCrypterReadModel>> GetByUploadConfigIdAsync(
        int uploadConfigId,
        CancellationToken cancellationToken = default
    )
    {
        var linkCrypterNamesByClassName = linkCrypterFactory
            .GetLinkCrypters()
            .ToDictionary(l => l.ClassName, l => l.Name);

        return await dbRead
            .UploadConfigLinkCrypters.Where(u => u.UploadConfigId == uploadConfigId)
            .Select(u => new UploadConfigLinkCrypterReadModel(
                u.Id,
                linkCrypterNamesByClassName[u.LinkCrypterRegistration.LinkCrypterClassName],
                u.LinkCrypterRegistration.Name,
                u.LinkCrypterRegistrationId,
                u.Password,
                u.LinkCrypterRegistration.IsActive
            ))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetLinkCrypterOptionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .LinkCrypterRegistrations.Where(l => l.IsActive)
            .ToDictionaryAsync(l => l.Id, l => l.Name, cancellationToken);
    }
}
