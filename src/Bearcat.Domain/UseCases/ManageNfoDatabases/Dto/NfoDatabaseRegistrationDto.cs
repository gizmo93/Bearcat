namespace Bearcat.Domain.UseCases.ManageNfoDatabases.Dto;

public record NfoDatabaseRegistrationDto(
    int Id,
    bool IsActive,
    string NfoDatabaseName,
    string NfoDatabaseClassName,
    IReadOnlyDictionary<string, string> Configuration
);
