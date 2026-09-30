namespace Bearcat.Abstractions.DistributionSite.Dto;

public sealed record DistributionSession(
    string BaseUrl,
    string UserAgent,
    IReadOnlyList<SessionCookie> Cookies
);
