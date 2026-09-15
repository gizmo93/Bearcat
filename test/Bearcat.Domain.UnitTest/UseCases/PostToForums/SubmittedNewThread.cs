namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public sealed record SubmittedNewThread(
    int RegistrationId,
    string TargetNodeId,
    string Title,
    IReadOnlyList<string> PrefixIds,
    string Body
);
