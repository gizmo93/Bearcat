namespace Bearcat.Abstractions.LinkCrypter.Results;

public record ReadContainerLinksResult(
    bool IsSuccess,
    IReadOnlyList<IReadOnlyList<string>> LinksPerMirror,
    bool HasBackupLinks,
    IReadOnlyList<string> ErrorMessages
);
