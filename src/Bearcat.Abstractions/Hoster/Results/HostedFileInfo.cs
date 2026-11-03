namespace Bearcat.Abstractions.Hoster.Results;

public record HostedFileInfo(bool IsOnline, string? FileName, long? SizeBytes);
