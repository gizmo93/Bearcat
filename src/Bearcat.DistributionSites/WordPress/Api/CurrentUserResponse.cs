namespace Bearcat.DistributionSites.WordPress.Api;

public sealed record CurrentUserResponse(IReadOnlyDictionary<string, bool>? Capabilities);
