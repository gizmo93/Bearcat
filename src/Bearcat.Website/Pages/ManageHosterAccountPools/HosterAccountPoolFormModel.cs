using Bearcat.Domain.UseCases.ManageHosterAccountPools.Dto;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;
using Humanizer;

namespace Bearcat.Website.Pages.ManageHosterAccountPools;

public class HosterAccountPoolFormModel
{
    public int? HosterAccountPoolId { get; set; }

    public string? Name { get; set; }

    public string? HosterClassName { get; set; }

    public string? HosterName { get; set; }

    public int MinimumFreeSpaceGb { get; set; }

    public List<HosterAccountPoolCandidateReadModel> MembersInPriorityOrder { get; set; } = [];

    public static HosterAccountPoolFormModel FromReadModel(HosterAccountPoolReadModel pool)
    {
        return new HosterAccountPoolFormModel
        {
            HosterAccountPoolId = pool.Id,
            Name = pool.Name,
            HosterClassName = pool.HosterClassName,
            HosterName = pool.HosterName,
            MinimumFreeSpaceGb = (int)Math.Round(pool.MinimumFreeSpaceBytes.Bytes().Gigabytes),
            MembersInPriorityOrder = pool
                .MembersInPriorityOrder.Select(member => new HosterAccountPoolCandidateReadModel(
                    member.HosterRegistrationId,
                    member.Name,
                    member.IsActive,
                    member.HasUnreadableSecrets
                ))
                .ToList(),
        };
    }

    public HosterAccountPoolInput ToInput()
    {
        return new HosterAccountPoolInput(
            Name ?? string.Empty,
            (long)ByteSize.FromGigabytes(MinimumFreeSpaceGb).Bytes,
            MembersInPriorityOrder.Select(member => member.HosterRegistrationId).ToList()
        );
    }
}
