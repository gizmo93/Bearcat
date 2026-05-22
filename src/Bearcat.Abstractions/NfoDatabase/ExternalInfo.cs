namespace Bearcat.Abstractions.NfoDatabase;

public record ExternalInfo(ExternalInfoType Type, string? Title, IReadOnlyList<Url> Urls);
