namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public sealed record SubmittedNewPost(
    int RegistrationId,
    string TargetNodeId,
    string Title,
    string Body
);
