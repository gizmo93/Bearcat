using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Abstractions.DistributionSite.Results;

public sealed record DistributionSiteLoginResult(
    DistributionSession? Session,
    string? ErrorMessage
);
