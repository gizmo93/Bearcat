namespace Bearcat.Abstractions.DistributionSite.Dto;

public sealed record ForumTargetId(string Value);

public sealed record ForumTargetNode(
    ForumTargetId Id,
    string Title,
    bool CanReceivePosts,
    IReadOnlyList<ForumTargetNode> Children
);
