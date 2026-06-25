namespace Bearcat.Abstractions.LinkCrypter.Results;

public record UpdateContainerResult(
    bool IsSuccess,
    string? ErrorMessage,
    string? StatusImageId = null
);
