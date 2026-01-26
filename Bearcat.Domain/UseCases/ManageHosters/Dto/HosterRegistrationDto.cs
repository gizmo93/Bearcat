namespace Bearcat.Domain.UseCases.ManageHosters.Dto;

public record HosterRegistrationDto(
    int Id,
    string Name,
    bool IsActive,
    string HosterName,
    string FullClassName,
    IReadOnlyDictionary<string, string> Configuration);
