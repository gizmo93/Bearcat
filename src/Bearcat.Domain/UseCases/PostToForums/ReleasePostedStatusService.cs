using Bearcat.Domain.UseCases.PostToForums.Repositories;

namespace Bearcat.Domain.UseCases.PostToForums;

public class ReleasePostedStatusService(
    AutoForumPostingPlanService planService,
    IAutoForumPostingRepository repository
)
{
    public async Task<bool> MarkReleasePostedWhenNoSiteIsLeftAsync(
        int releaseId,
        CancellationToken cancellationToken
    )
    {
        var currentPlan = await planService.GetPlanAsync(releaseId, cancellationToken);

        if (
            currentPlan.IsBlocked
            || currentPlan.PendingSites.Count > 0
            || currentPlan.UpdatableSites.Count > 0
        )
        {
            return false;
        }

        await repository.MarkReleasePostedAsync(releaseId, cancellationToken);

        return true;
    }
}
