using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Dto;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class LinkCrypterRegistrationReadRepository(
    IBearcatReadDbContext dbRead,
    ILinkCrypterFactory linkCrypterFactory) : ILinkCrypterRegistrationReadRepository
{
    public async Task<IReadOnlyList<LinkCrypterRegistrationDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var cryptersByClassName = linkCrypterFactory.GetByClassName();

        return await dbRead.LinkCrypterRegistrations
            .Select(l => new LinkCrypterRegistrationDto(
                l.Id,
                l.Name,
                l.LinkCrypterClassName,
                cryptersByClassName[l.LinkCrypterClassName].GetType().Name,
                l.SerializedConfig,
                cryptersByClassName[l.LinkCrypterClassName]
                    .DeserializeConfig(l.SerializedConfig)
                    .ToDictionary(),
                l.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<LinkCrypterRegistrationDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var cryptersByClassName = linkCrypterFactory.GetByClassName();

        return await dbRead.LinkCrypterRegistrations
            .Where(l => l.Id == id)
            .Select(l => new LinkCrypterRegistrationDto(
                l.Id,
                l.Name,
                cryptersByClassName[l.LinkCrypterClassName].GetType().Name,
                l.LinkCrypterClassName,
                l.SerializedConfig,
                cryptersByClassName[l.LinkCrypterClassName]
                    .DeserializeConfig(l.SerializedConfig)
                    .ToDictionary(),
                l.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
