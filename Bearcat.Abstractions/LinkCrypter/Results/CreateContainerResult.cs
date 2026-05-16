namespace Bearcat.Abstractions.LinkCrypter.Results;

public record CreateContainerResult(
    bool IsSuccess,
    string? ContainerLink,
    string? ExternalReference,
    IReadOnlyList<string> ErrorMessages
);
