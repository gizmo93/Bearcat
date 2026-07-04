namespace Bearcat.Abstractions.Hoster.Dto;

public record FileUrlToCheckDto(string Url, string? ExternalId, string? HosterFolderId = null);
