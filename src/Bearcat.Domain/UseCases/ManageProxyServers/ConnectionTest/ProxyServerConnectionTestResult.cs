namespace Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;

public record ProxyServerConnectionTestResult(
    ProxyServerConnectionTestOutcome Outcome,
    string? TechnicalDetail
)
{
    public bool IsSuccess => Outcome == ProxyServerConnectionTestOutcome.Success;
}
