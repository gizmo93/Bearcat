namespace Bearcat.DistributionSites.WordPress.Api;

public sealed record CreatePostRequest(
    string Title,
    string Status,
    IReadOnlyList<int> Categories,
    string Content
);
