namespace Bearcat.Abstractions.LinkCrypter.Results;

public record CreateContainerResult(
    bool IsSuccess,
    string? ContainerLink,
    IReadOnlyList<string> ErrorMessages);
