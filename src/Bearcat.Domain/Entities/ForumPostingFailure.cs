using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ForumPostingFailure
{
    public int Id { get; set; }

    public int? ReleaseId { get; set; }

    public Release? Release { get; set; }

    public int? ReleaseCollectionId { get; set; }

    public ReleaseCollection? ReleaseCollection { get; set; }

    public int DistributionSiteRegistrationId { get; set; }

    public DistributionSiteRegistration DistributionSiteRegistration { get; set; } = null!;

    public int FailedAttemptCount { get; set; }

    public DateTime? NextAttemptAt { get; set; }

    public string LastErrorMessage { get; set; } = null!;

    public ForumPostingFailureState State { get; set; }
}
