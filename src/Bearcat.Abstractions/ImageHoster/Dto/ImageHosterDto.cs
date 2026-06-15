namespace Bearcat.Abstractions.ImageHoster.Dto;

public record ImageHosterDto(
    string Name,
    string ClassName,
    IReadOnlyList<string> ConfigurationKeys,
    bool SupportsLogin
);
