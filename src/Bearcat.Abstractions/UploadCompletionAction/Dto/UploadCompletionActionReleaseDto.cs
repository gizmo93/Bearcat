namespace Bearcat.Abstractions.UploadCompletionAction.Dto;

public sealed record UploadCompletionActionReleaseDto(
    string Name,
    string? ContentType,
    string? Platform,
    string? Title,
    string? Genre,
    string? Description,
    int? RuntimeMinutes,
    string? VideoType,
    string? VideoCodec,
    string? AudioType,
    string? AudioCodec,
    int? AudioChannels,
    IReadOnlyList<string> AudioLanguages,
    string? Nfo
);
