using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class PostedLocationWriteRepository(IBearcatWriteDbContext dbWrite)
    : IPostedLocationWriteRepository
{
    public async Task<PostedLocation> GetByIdAsync(
        int postedLocationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.PostedLocations.FirstAsync(
            location => location.Id == postedLocationId,
            cancellationToken
        );
    }

    public void Add(PostedLocation postedLocation)
    {
        dbWrite.Add(postedLocation);
    }

    public void Remove(PostedLocation postedLocation)
    {
        dbWrite.Remove(postedLocation);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
