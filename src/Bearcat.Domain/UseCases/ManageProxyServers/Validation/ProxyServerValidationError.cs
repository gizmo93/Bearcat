namespace Bearcat.Domain.UseCases.ManageProxyServers.Validation;

public enum ProxyServerValidationError
{
    NameRequired = 1,
    NameTooLong = 2,
    NameAlreadyExists = 3,
    HostRequired = 4,
    HostTooLong = 5,
    HostInvalid = 6,
    PortOutOfRange = 7,
    UsernameTooLong = 8,
    UsernameRequiredForPassword = 9,
}
