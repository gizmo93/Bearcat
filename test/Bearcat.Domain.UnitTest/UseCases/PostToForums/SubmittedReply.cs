namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public sealed record SubmittedReply(int RegistrationId, string ThreadUrl, string Body);
