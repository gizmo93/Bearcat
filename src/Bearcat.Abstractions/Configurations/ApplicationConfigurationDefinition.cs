namespace Bearcat.Abstractions.Configurations;

public sealed record ApplicationConfigurationDefinition(
    string Key,
    string DisplayName,
    string? Description,
    Type ConfigurationType,
    IReadOnlyList<ApplicationConfigurationPropertyDefinition> Properties
);
