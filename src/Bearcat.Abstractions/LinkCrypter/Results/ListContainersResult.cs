namespace Bearcat.Abstractions.LinkCrypter.Results;

public record ListContainersResult(
    bool IsSuccess,
    IReadOnlyList<ListedContainer> Containers,
    IReadOnlyList<string> ErrorMessages
);
