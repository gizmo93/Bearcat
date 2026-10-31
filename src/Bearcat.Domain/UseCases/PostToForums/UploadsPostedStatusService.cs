using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums;

public class UploadsPostedStatusService(
    AutoForumPostingPlanService planService,
    IAutoForumPostingRepository repository
)
{
    public async Task<bool> MarkUploadsPostedWhenNoSiteIsLeftAsync(
        ForumPostTemplateType entityType,
        int entityId,
        CancellationToken cancellationToken
    )
    {
        var currentPlan = await planService.GetPlanAsync(entityType, entityId, cancellationToken);

        if (
            currentPlan.IsBlocked
            || currentPlan.PendingSites.Count > 0
            || currentPlan.UpdatableSites.Count > 0
        )
        {
            return false;
        }

        await repository.MarkUploadsPostedAsync(entityType, entityId, cancellationToken);

        return true;
    }
}
