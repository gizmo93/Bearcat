namespace Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;

public interface IProxyServerConnectionTester
{
    Task<ProxyServerConnectionTestResult> TestAsync(
        ProxyServerConnectionTestRequest request,
        CancellationToken cancellationToken
    );
}
