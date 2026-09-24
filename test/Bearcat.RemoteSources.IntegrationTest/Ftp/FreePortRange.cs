using System.Net;
using System.Net.Sockets;

namespace Bearcat.RemoteSources.IntegrationTest.Ftp;

public static class FreePortRange
{
    private const int LowestPort = 40000;

    private const int HighestPort = 60000;

    private const int MaximumAttempts = 100;

    public static int FindStart(int count)
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var start = Random.Shared.Next(LowestPort, HighestPort - count);
            if (Enumerable.Range(start, count).All(IsFree))
            {
                return start;
            }
        }

        throw new InvalidOperationException($"No free range of {count} ports found");
    }

    private static bool IsFree(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
