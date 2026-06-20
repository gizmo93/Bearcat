namespace Bearcat.Abstractions.DistributionSite.Results;

public sealed record TryLoginResult(bool IsSuccess, string? ErrorMessage = null);
