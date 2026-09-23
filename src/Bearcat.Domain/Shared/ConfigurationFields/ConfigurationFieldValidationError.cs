namespace Bearcat.Domain.Shared.ConfigurationFields;

public enum ConfigurationFieldValidationError
{
    UnknownField = 1,
    Required = 2,
    InvalidValue = 3,
    NotAnInteger = 4,
    InvalidOption = 5,
}
