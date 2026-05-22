namespace Bearcat.Abstractions.NfoDatabase;

public record NfoDatabaseDto(
    string Name,
    string ClassName,
    IReadOnlyList<string> ConfigurationKeys
);
