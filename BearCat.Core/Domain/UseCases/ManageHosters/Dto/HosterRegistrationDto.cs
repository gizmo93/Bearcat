namespace BearCat.Core.Domain.UseCases.ManageHosters.Dto;

public record HosterRegistrationDto(
    int Id,
    string Name,
    bool IsActive,
    string HosterName);
