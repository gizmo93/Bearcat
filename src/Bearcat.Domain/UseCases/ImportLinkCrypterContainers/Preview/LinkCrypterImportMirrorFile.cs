namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public sealed record LinkCrypterImportMirrorFile(
    string Link,
    string? OwnFormatLink,
    bool? IsOnline,
    string? FileName,
    long? SizeBytes
);
