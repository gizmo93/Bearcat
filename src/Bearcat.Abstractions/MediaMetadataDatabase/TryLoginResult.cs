namespace Bearcat.Abstractions.MediaMetadataDatabase;

public record TryLoginResult(bool IsSuccess, string? ErrorMessage);
