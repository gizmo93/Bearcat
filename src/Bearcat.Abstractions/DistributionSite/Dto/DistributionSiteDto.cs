namespace Bearcat.Abstractions.DistributionSite.Dto;

public sealed record DistributionSiteDto(
    string Name,
    string ClassName,
    DistributionSiteKind Kind,
    IReadOnlyList<string> ConfigurationKeys
);
