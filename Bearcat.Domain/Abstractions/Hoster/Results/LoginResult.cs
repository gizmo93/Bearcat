namespace Bearcat.Domain.Abstractions.Hoster.Results;

public record TryLoginResult(bool IsSuccess, string? ErrorMessage);
