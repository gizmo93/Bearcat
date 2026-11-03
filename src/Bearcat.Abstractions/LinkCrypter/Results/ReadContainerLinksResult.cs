namespace Bearcat.Abstractions.LinkCrypter.Results;

public record ReadContainerLinksResult(
    bool IsSuccess,
    IReadOnlyDictionary<int, IReadOnlyList<string>> LinksPerMirrorNumber,
    bool HasBackupLinks,
    IReadOnlyList<string> ErrorMessages
);
