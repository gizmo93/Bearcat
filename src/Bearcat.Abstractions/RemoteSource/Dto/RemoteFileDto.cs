namespace Bearcat.Abstractions.RemoteSource.Dto;

public sealed record RemoteFileDto(string RelativePath, string FullPath, long SizeBytes);
