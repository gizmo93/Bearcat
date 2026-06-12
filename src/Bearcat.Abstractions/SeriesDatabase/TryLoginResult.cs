namespace Bearcat.Abstractions.SeriesDatabase;

public record TryLoginResult(bool IsSuccess, string? ErrorMessage);
