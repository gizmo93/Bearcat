using System.Net.Sockets;
using System.Text;
using Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Infrastructure.Network;

public sealed class ProxyServerConnectionTester : IProxyServerConnectionTester
{
    public const string HttpConnectTarget = "bearcat-proxy-test.invalid:443";

    private const byte Socks5Version = 0x05;
    private const byte Socks5NoAuthenticationMethod = 0x00;
    private const byte Socks5UsernamePasswordMethod = 0x02;
    private const byte Socks5NoAcceptableMethods = 0xFF;
    private const byte Socks5UsernamePasswordVersion = 0x01;
    private const byte Socks5UsernamePasswordSuccess = 0x00;
    private const int Socks5MaximumCredentialLength = 255;
    private const int HttpProxyAuthenticationRequiredStatusCode = 407;
    private const int MaximumHttpStatusLineLength = 4096;

    private static readonly byte[] HttpVersionPrefix = "HTTP/1."u8.ToArray();

    public async Task<ProxyServerConnectionTestResult> TestAsync(
        ProxyServerConnectionTestRequest request,
        CancellationToken cancellationToken
    )
    {
        using var tcpClient = new TcpClient();

        try
        {
            await tcpClient.ConnectAsync(request.Host, request.Port, cancellationToken);
        }
        catch (SocketException exception)
        {
            return CreateResult(ProxyServerConnectionTestOutcome.NotReachable, exception.Message);
        }

        await using var stream = tcpClient.GetStream();

        try
        {
            return request.ProxyType switch
            {
                ProxyType.Http => await TestHttpProxyAsync(stream, request, cancellationToken),
                ProxyType.Socks5 => await TestSocks5ProxyAsync(stream, request, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(request),
                    request.ProxyType,
                    null
                ),
            };
        }
        catch (IOException exception)
        {
            return CreateResult(
                ProxyServerConnectionTestOutcome.WrongProtocol,
                $"The server closed the connection before completing the proxy handshake: {exception.Message}"
            );
        }
    }

    private static async Task<ProxyServerConnectionTestResult> TestHttpProxyAsync(
        NetworkStream stream,
        ProxyServerConnectionTestRequest request,
        CancellationToken cancellationToken
    )
    {
        var hasCredentials = request.Username is not null;
        var connectRequest = new StringBuilder()
            .Append($"CONNECT {HttpConnectTarget} HTTP/1.1\r\n")
            .Append($"Host: {HttpConnectTarget}\r\n");

        if (hasCredentials)
        {
            var encodedCredentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{request.Username}:{request.Password}")
            );
            connectRequest.Append($"Proxy-Authorization: Basic {encodedCredentials}\r\n");
        }

        connectRequest.Append("\r\n");

        await stream.WriteAsync(
            Encoding.ASCII.GetBytes(connectRequest.ToString()),
            cancellationToken
        );

        var statusLine = await ReadHttpStatusLineAsync(stream, cancellationToken);
        var statusLineParts = statusLine?.Split(' ', 3);

        if (
            statusLineParts is not { Length: >= 2 }
            || !int.TryParse(statusLineParts[1], out var statusCode)
        )
        {
            return CreateResult(
                ProxyServerConnectionTestOutcome.WrongProtocol,
                "The server did not answer the CONNECT request with an HTTP response."
            );
        }

        if (statusCode != HttpProxyAuthenticationRequiredStatusCode)
        {
            return CreateResult(ProxyServerConnectionTestOutcome.Success, statusLine);
        }

        return hasCredentials
            ? CreateResult(ProxyServerConnectionTestOutcome.AuthenticationFailed, statusLine)
            : CreateResult(ProxyServerConnectionTestOutcome.AuthenticationRequired, statusLine);
    }

    private static async Task<string?> ReadHttpStatusLineAsync(
        NetworkStream stream,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[MaximumHttpStatusLineLength];
        var length = 0;

        while (length < buffer.Length)
        {
            var readByteCount = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);

            if (readByteCount == 0)
            {
                return null;
            }

            length += readByteCount;

            var comparedLength = Math.Min(length, HttpVersionPrefix.Length);
            if (
                !buffer
                    .AsSpan(0, comparedLength)
                    .SequenceEqual(HttpVersionPrefix.AsSpan(0, comparedLength))
            )
            {
                return null;
            }

            var lineEnd = buffer.AsSpan(0, length).IndexOf("\r\n"u8);
            if (lineEnd >= 0)
            {
                return Encoding.ASCII.GetString(buffer, 0, lineEnd);
            }
        }

        return null;
    }

    private static async Task<ProxyServerConnectionTestResult> TestSocks5ProxyAsync(
        NetworkStream stream,
        ProxyServerConnectionTestRequest request,
        CancellationToken cancellationToken
    )
    {
        var hasCredentials = request.Username is not null;
        byte[] greeting = hasCredentials
            ? [Socks5Version, 2, Socks5NoAuthenticationMethod, Socks5UsernamePasswordMethod]
            : [Socks5Version, 1, Socks5NoAuthenticationMethod];

        await stream.WriteAsync(greeting, cancellationToken);

        var reply = new byte[2];
        await stream.ReadExactlyAsync(reply, cancellationToken);

        if (reply[0] != Socks5Version)
        {
            return CreateResult(
                ProxyServerConnectionTestOutcome.WrongProtocol,
                $"The server answered with version byte 0x{reply[0]:X2} instead of the SOCKS5 version 0x05."
            );
        }

        return reply[1] switch
        {
            Socks5NoAuthenticationMethod => CreateResult(
                ProxyServerConnectionTestOutcome.Success,
                "The SOCKS5 proxy server accepts connections without authentication."
            ),
            Socks5UsernamePasswordMethod when hasCredentials => await AuthenticateSocks5Async(
                stream,
                request.Username!,
                request.Password ?? string.Empty,
                cancellationToken
            ),
            Socks5NoAcceptableMethods when hasCredentials => CreateResult(
                ProxyServerConnectionTestOutcome.AuthenticationFailed,
                "The SOCKS5 proxy server accepts neither username and password nor anonymous connections."
            ),
            Socks5NoAcceptableMethods => CreateResult(
                ProxyServerConnectionTestOutcome.AuthenticationRequired,
                "The SOCKS5 proxy server does not accept anonymous connections."
            ),
            _ => CreateResult(
                ProxyServerConnectionTestOutcome.WrongProtocol,
                $"The server selected the authentication method 0x{reply[1]:X2}, which was not offered."
            ),
        };
    }

    private static async Task<ProxyServerConnectionTestResult> AuthenticateSocks5Async(
        NetworkStream stream,
        string username,
        string password,
        CancellationToken cancellationToken
    )
    {
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        if (
            usernameBytes.Length > Socks5MaximumCredentialLength
            || passwordBytes.Length > Socks5MaximumCredentialLength
        )
        {
            return CreateResult(
                ProxyServerConnectionTestOutcome.AuthenticationFailed,
                $"SOCKS5 only supports usernames and passwords up to {Socks5MaximumCredentialLength} bytes."
            );
        }

        byte[] authenticationRequest =
        [
            Socks5UsernamePasswordVersion,
            (byte)usernameBytes.Length,
            .. usernameBytes,
            (byte)passwordBytes.Length,
            .. passwordBytes,
        ];

        await stream.WriteAsync(authenticationRequest, cancellationToken);

        var reply = new byte[2];
        await stream.ReadExactlyAsync(reply, cancellationToken);

        return reply[1] == Socks5UsernamePasswordSuccess
            ? CreateResult(
                ProxyServerConnectionTestOutcome.Success,
                "The SOCKS5 proxy server accepted the username and password."
            )
            : CreateResult(
                ProxyServerConnectionTestOutcome.AuthenticationFailed,
                $"The SOCKS5 proxy server rejected the username and password with status 0x{reply[1]:X2}."
            );
    }

    private static ProxyServerConnectionTestResult CreateResult(
        ProxyServerConnectionTestOutcome outcome,
        string? technicalDetail
    )
    {
        return new ProxyServerConnectionTestResult(outcome, technicalDetail);
    }
}
