using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.UseCases.ManageApplicationConfigurations;

public sealed record ApplicationConfigurationPropertyDto(
    string ConfigurationKey,
    string Name,
    string DisplayName,
    string? Description,
    Type ValueType,
    object? DefaultValue,
    object? CurrentValue,
    bool IsOverridden,
    IReadOnlyList<string> Options,
    ApplicationConfigurationUnit? Unit
);
