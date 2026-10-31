namespace Bearcat.Abstractions.LinkCrypter.Results;

public record UpdateContainerResult(
    UpdateContainerOutcome Outcome,
    string? ErrorMessage,
    string? StatusImageId = null
);
