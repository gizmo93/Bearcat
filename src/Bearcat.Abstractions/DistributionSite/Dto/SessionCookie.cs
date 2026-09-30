namespace Bearcat.Abstractions.DistributionSite.Dto;

public sealed record SessionCookie(string Name, string Value, string Domain, string Path);
