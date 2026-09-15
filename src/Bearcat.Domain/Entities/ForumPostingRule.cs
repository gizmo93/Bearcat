using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ForumPostingRule
{
    public int Id { get; set; }

    public int DistributionSiteRegistrationId { get; set; }

    public DistributionSiteRegistration DistributionSiteRegistration { get; set; } = null!;

    public int SortOrder { get; set; }

    public string Name { get; set; } = null!;

    public string ConditionJson { get; set; } = null!;

    public string TargetNodeId { get; set; } = null!;

    public string TargetPathSnapshot { get; set; } = null!;

    public string? ThreadPrefixId { get; set; }

    public int ForumPostTemplateId { get; set; }

    public ForumPostTemplate ForumPostTemplate { get; set; } = null!;

    public ForumPostPostMode PostMode { get; set; } =
        ForumPostPostMode.ReplyToExistingElseNewThread;

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
