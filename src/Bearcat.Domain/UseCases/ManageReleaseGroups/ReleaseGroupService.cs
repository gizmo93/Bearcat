using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;

namespace Bearcat.Domain.UseCases.ManageReleaseGroups;

public class ReleaseGroupService(IReleaseGroupWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        string name,
        bool enableAutomaticReuploads,
        int numberOfHoursUntilReupload,
        int? qualityProfileId,
        CancellationToken cancellationToken = default
    )
    {
        Validate(name, numberOfHoursUntilReupload);

        var releaseGroup = new ReleaseGroup
        {
            Name = name.Trim(),
            EnableAutomaticReuploads = enableAutomaticReuploads,
            NumberOfHoursUntilReupload = numberOfHoursUntilReupload,
            QualityProfileId = qualityProfileId,
        };

        writeRepository.Add(releaseGroup);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return releaseGroup.Id;
    }

    public async Task UpdateAsync(
        int releaseGroupId,
        string name,
        bool enableAutomaticReuploads,
        int numberOfHoursUntilReupload,
        int? qualityProfileId,
        CancellationToken cancellationToken = default
    )
    {
        Validate(name, numberOfHoursUntilReupload);

        var releaseGroup = await writeRepository.GetByIdAsync(releaseGroupId, cancellationToken);
        releaseGroup.Name = name.Trim();
        releaseGroup.EnableAutomaticReuploads = enableAutomaticReuploads;
        releaseGroup.NumberOfHoursUntilReupload = numberOfHoursUntilReupload;
        releaseGroup.QualityProfileId = qualityProfileId;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int releaseGroupId, CancellationToken cancellationToken = default)
    {
        if (await writeRepository.HasAssignedReleasesAsync(releaseGroupId, cancellationToken))
        {
            throw new InvalidOperationException(
                "Release groups with assigned releases cannot be deleted."
            );
        }

        var releaseGroup = await writeRepository.GetByIdAsync(releaseGroupId, cancellationToken);
        writeRepository.Remove(releaseGroup);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(string name, int numberOfHoursUntilReupload)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (numberOfHoursUntilReupload < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numberOfHoursUntilReupload),
                "Number of hours until reupload must be zero or greater."
            );
        }
    }
}
