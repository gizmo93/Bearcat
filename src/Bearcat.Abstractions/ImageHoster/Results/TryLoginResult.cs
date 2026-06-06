namespace Bearcat.Abstractions.ImageHoster.Results;

public record TryLoginResult(bool IsSuccess, string? ErrorMessage = null);
