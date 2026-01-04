using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database;

public interface IBearcatReadDbContext
{
    public DbSet<HosterRegistration> HosterRegistrations { get; set; }
}
