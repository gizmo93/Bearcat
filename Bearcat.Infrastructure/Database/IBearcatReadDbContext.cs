using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database;

public interface IBearcatReadDbContext
{
    public DbSet<HosterRegistration> HosterRegistrations { get; set; }
    public DbSet<Release> Releases { get; set; }
    public DbSet<Archive> Archives { get; set; }
    public DbSet<ArchiveConfig> ArchiveConfigs { get; set; }
    public DbSet<ArchiveFile> ArchiveFiles { get; set; }
    public DbSet<Upload> Uploads { get; set; }
    public DbSet<UploadConfig> UploadConfigs { get; set; }
    public DbSet<UploadedFile> UploadedFiles { get; set; }
    public DbSet<LinkCrypterRegistration> LinkCrypterRegistrations { get; set; }
}
