namespace Bearcat.Abstractions.Hoster.Dto;

public record DownloadFileDto(string HosterFileLink, string? ExternalId, long? ExpectedSizeBytes);
