using Bearcat.Domain.Shared.Entities;

namespace Bearcat.Domain.Entities;

public class DistributionSiteRegistration : IEntityWithEncryptedSecrets, IActivatableEntity
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string DistributionSiteClassName { get; set; }

    public required string SerializedConfig { get; set; }

    public bool IsActive { get; set; }

    public bool EnableAutomaticPosting { get; set; }

    public bool StripDotsForThreadSearch { get; set; } = true;

    public int MinimumSecondsBetweenPosts { get; set; } = 30;

    public List<string> RequiredHosterClassNames { get; set; } = [];

    public List<string> ForbiddenHosterClassNames { get; set; } = [];

    public List<string> RequiredLinkCrypterClassNames { get; set; } = [];

    public List<string> ForbiddenLinkCrypterClassNames { get; set; } = [];

    public List<string> RequiredImageHosterClassNames { get; set; } = [];

    public List<string> ForbiddenImageHosterClassNames { get; set; } = [];

    public string? EncryptedSession { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public List<ForumPostingRule> PostingRules { get; set; } = [];

    public string GetEncryptedSecrets() => SerializedConfig;

    public string GetRegistrationTypeName() => "Distribution site";

    public string? GetRegistrationName() => Name;
}
