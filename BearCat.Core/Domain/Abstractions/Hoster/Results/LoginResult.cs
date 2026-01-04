namespace BearCat.Core.Domain.Abstractions.Hoster.Results;

public record TryLoginResult(bool IsSuccess, string? ErrorMessage);
