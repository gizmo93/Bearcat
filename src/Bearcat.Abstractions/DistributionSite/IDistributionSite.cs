using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Abstractions.DistributionSite.Results;

namespace Bearcat.Abstractions.DistributionSite;

public interface IDistributionSite
{
    string Name { get; }

    PostContentFormat ContentFormat { get; }

    IReadOnlyList<ConfigurationField> ConfigurationFields { get; }

    IDistributionSiteConfig DeserializeConfig(string serializedConfig);

    string GetBaseUrl(IDistributionSiteConfig config);

    Task<DistributionSiteLoginResult> LogInAsync(
        IDistributionSiteConfig config,
        CancellationToken cancellationToken
    );

    Task<bool> IsSessionValidAsync(
        DistributionSession session,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<ForumTargetNode>> GetTargetHierarchyAsync(
        DistributionSession session,
        CancellationToken cancellationToken
    );

    Task<SubmittedPost> EditPostAsync(
        IDistributionSiteConfig config,
        DistributionSession session,
        string postedUrl,
        string body,
        CancellationToken cancellationToken
    );

    ForumTargetId CreateForumTargetIdFromStoredValue(
        DistributionSession session,
        string storedTargetId
    );
}
