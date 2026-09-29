using System.Net.Sockets;
using Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;

namespace Bearcat.Infrastructure.Network;

public sealed class TcpConnectionOpener : ITcpConnectionOpener
{
    public async Task OpenConnectionAsync(
        string host,
        int port,
        CancellationToken cancellationToken
    )
    {
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(host, port, cancellationToken);
    }
}
