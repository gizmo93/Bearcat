namespace Bearcat.Abstractions.DistributionSite.Dto;

public sealed record DistributionSiteConfigurationField(
    string Key,
    string? LabelResourceKey = null,
    string? Placeholder = null,
    bool IsSecret = false,
    bool PrefillOnEdit = false
);
