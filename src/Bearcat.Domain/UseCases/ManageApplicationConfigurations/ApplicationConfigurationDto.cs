namespace Bearcat.Domain.UseCases.ManageApplicationConfigurations;

public sealed record ApplicationConfigurationDto(
    string DisplayName,
    string? Description,
    IReadOnlyList<ApplicationConfigurationPropertyDto> Properties
);

public sealed record ApplicationConfigurationPropertyDto(
    string ConfigurationKey,
    string Name,
    string DisplayName,
    string? Description,
    Type ValueType,
    object? DefaultValue,
    object? CurrentValue,
    bool IsOverridden,
    IReadOnlyList<string> Options
);
