namespace Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;

public enum ProxyServerConnectionTestOutcome
{
    Success = 1,
    NotReachable = 2,
    WrongProtocol = 3,
    AuthenticationFailed = 4,
    AuthenticationRequired = 5,
    TimedOut = 6,
    UnreadablePassword = 7,
}
