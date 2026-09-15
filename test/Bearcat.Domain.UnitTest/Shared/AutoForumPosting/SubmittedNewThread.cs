namespace Bearcat.Domain.UnitTest.Shared.AutoForumPosting;

public sealed record SubmittedNewThread(
    int RegistrationId,
    string TargetNodeId,
    string Title,
    IReadOnlyList<string> PrefixIds,
    string Body
);
