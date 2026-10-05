namespace Bearcat.Domain.UseCases.ManageApplicationConfigurations;

public sealed record ApplicationConfigurationDto(
    string Key,
    string DisplayName,
    string? Description,
    IReadOnlyList<ApplicationConfigurationPropertyDto> Properties
);
