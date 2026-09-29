namespace Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;

public interface ITcpConnectionOpener
{
    Task OpenConnectionAsync(string host, int port, CancellationToken cancellationToken);
}
