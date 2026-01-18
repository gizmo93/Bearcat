using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.UseCases.ManageReleases;

public class ReleaseService(IReleaseWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        string name,
        string releaseFolderPath,
        ReleaseType releaseType,
        CancellationToken cancellationToken = default)
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = releaseType,
            ReleaseFolderPath = releaseFolderPath,
        };
        
        writeRepository.Add(release);
        await writeRepository.SaveChangesAsync(cancellationToken);
        
        return release.Id;
    }
    
    public async Task UpdateAsync(int releaseId, string name, CancellationToken cancellationToken)
    {
        var release = await writeRepository.GetByIdAsync(releaseId, cancellationToken);
        release.Name = name;
        
        await writeRepository.SaveChangesAsync(cancellationToken);
    }
    
    public async Task DeleteAsync(int releaseId, CancellationToken cancellationToken)
    {
        var release = await writeRepository.GetByIdAsync(releaseId, cancellationToken);
        writeRepository.Remove(release);
        
        await writeRepository.SaveChangesAsync(cancellationToken);
    }
}
