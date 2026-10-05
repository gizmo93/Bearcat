using Bearcat.Domain.UseCases.ManageApplicationConfigurations;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Settings;

public sealed record ConfigurationSettingSaveRequest(
    ApplicationConfigurationPropertyDto Setting,
    object? Value
);
