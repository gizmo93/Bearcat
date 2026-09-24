namespace Bearcat.Abstractions.ConfigurationFields;

public sealed record ConfigurationField(
    string Key,
    ConfigurationFieldType Type,
    bool IsRequired,
    object? DefaultValue = null,
    IReadOnlyList<string>? Options = null
);
