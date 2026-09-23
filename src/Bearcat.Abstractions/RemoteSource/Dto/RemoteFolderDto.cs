namespace Bearcat.Abstractions.RemoteSource.Dto;

public sealed record RemoteFolderDto(string Name, string FullPath, DateTime? ModifiedAt);
