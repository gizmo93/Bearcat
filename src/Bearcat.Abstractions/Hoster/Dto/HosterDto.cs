namespace Bearcat.Abstractions.Hoster.Dto;

public record HosterDto(
    string Name,
    string HosterClassName,
    IReadOnlyList<string> ConfigurationKeys
);
