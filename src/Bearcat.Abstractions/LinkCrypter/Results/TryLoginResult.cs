namespace Bearcat.Abstractions.LinkCrypter.Results;

public record TryLoginResult(bool IsSuccess, string? ErrorMessage = null);
