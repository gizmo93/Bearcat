using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BearCat.Core.Infrastructure.Database;

public interface IBearcatWriteDbContext
{
    DbSet<HosterRegistration> HosterRegistrations { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    EntityEntry Add(object entity);

    EntityEntry Remove(object entity);
}
