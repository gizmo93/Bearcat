namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.Validation;

public enum HosterAccountPoolValidationError
{
    NameRequired = 1,
    NameTooLong = 2,
    NameAlreadyExists = 3,
    MinimumFreeSpaceNegative = 4,
}
