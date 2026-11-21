namespace Bearcat.DistributionSites.WordPress.Api;

public sealed record CurrentUserResponse(int Id, IReadOnlyDictionary<string, bool>? Capabilities);
