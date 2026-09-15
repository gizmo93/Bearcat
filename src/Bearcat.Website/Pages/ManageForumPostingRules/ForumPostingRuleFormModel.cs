using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageForumPostingRules;

public sealed class ForumPostingRuleFormModel
{
    public int? ForumPostingRuleId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? TargetNodeId { get; set; }

    public string TargetPathSnapshot { get; set; } = string.Empty;

    public string ThreadPrefixId { get; set; } = string.Empty;

    public int ForumPostTemplateId { get; set; }

    public ForumPostPostMode PostMode { get; set; } =
        ForumPostPostMode.ReplyToExistingElseNewThread;

    public bool StripDotsForThreadSearch { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public bool IsEdit => ForumPostingRuleId.HasValue;
}
