using System.Net;
using System.Net.Sockets;
using System.Text;
using Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Network;
using Shouldly;

namespace Bearcat.Infrastructure.UnitTest.Network;

public class ProxyServerConnectionTesterTest
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private TcpListener fakeServer = null!;
    private int fakeServerPort;
    private ProxyServerConnectionTester tester = null!;

    [SetUp]
    public void SetUp()
    {
        fakeServer = new TcpListener(IPAddress.Loopback, 0);
        fakeServer.Start();
        fakeServerPort = ((IPEndPoint)fakeServer.LocalEndpoint).Port;
        tester = new ProxyServerConnectionTester();
    }

    [TearDown]
    public void TearDown()
    {
        fakeServer.Dispose();
    }

    [Test]
    public async Task TestAsync_Socks5ServerAcceptsCredentials_ReturnsSuccessAndSendsCredentials()
    {
        // Arrange
        byte[] greeting = [];
        byte[] authenticationRequest = [];
        var server = RunFakeServerAsync(async stream =>
        {
            greeting = await ReadBytesAsync(stream, 4);
            await stream.WriteAsync(new byte[] { 0x05, 0x02 });
            authenticationRequest = await ReadBytesAsync(stream, 14);
            await stream.WriteAsync(new byte[] { 0x01, 0x00 });
        });

        // Act
        var result = await TestAsync(ProxyType.Socks5, "alice", "secret");

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.Success);
        greeting.ShouldBe(new byte[] { 0x05, 0x02, 0x00, 0x02 });
        authenticationRequest.ShouldBe([
            0x01,
            0x05,
            .. "alice"u8.ToArray(),
            0x06,
            .. "secret"u8.ToArray(),
        ]);
    }

    [Test]
    public async Task TestAsync_Socks5ServerWithoutAuthentication_ReturnsSuccess()
    {
        // Arrange
        byte[] greeting = [];
        var server = RunFakeServerAsync(async stream =>
        {
            greeting = await ReadBytesAsync(stream, 3);
            await stream.WriteAsync(new byte[] { 0x05, 0x00 });
        });

        // Act
        var result = await TestAsync(ProxyType.Socks5, username: null, password: null);

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.Success);
        greeting.ShouldBe(new byte[] { 0x05, 0x01, 0x00 });
    }

    [Test]
    public async Task TestAsync_Socks5ServerRejectsCredentials_ReturnsAuthenticationFailed()
    {
        // Arrange
        var server = RunFakeServerAsync(async stream =>
        {
            await ReadBytesAsync(stream, 4);
            await stream.WriteAsync(new byte[] { 0x05, 0x02 });
            await ReadBytesAsync(stream, 13);
            await stream.WriteAsync(new byte[] { 0x01, 0x01 });
        });

        // Act
        var result = await TestAsync(ProxyType.Socks5, "alice", "wrong");

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.AuthenticationFailed);
    }

    [Test]
    public async Task TestAsync_Socks5ServerAcceptsNoOfferedMethodWithoutCredentials_ReturnsAuthenticationRequired()
    {
        // Arrange
        var server = RunFakeServerAsync(async stream =>
        {
            await ReadBytesAsync(stream, 3);
            await stream.WriteAsync(new byte[] { 0x05, 0xFF });
        });

        // Act
        var result = await TestAsync(ProxyType.Socks5, username: null, password: null);

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.AuthenticationRequired);
    }

    [Test]
    public async Task TestAsync_Socks5ServerAcceptsNoOfferedMethodWithCredentials_ReturnsAuthenticationFailed()
    {
        // Arrange
        var server = RunFakeServerAsync(async stream =>
        {
            await ReadBytesAsync(stream, 4);
            await stream.WriteAsync(new byte[] { 0x05, 0xFF });
        });

        // Act
        var result = await TestAsync(ProxyType.Socks5, "alice", "secret");

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.AuthenticationFailed);
    }

    [Test]
    public async Task TestAsync_HttpProxyAnswers407WithCredentials_ReturnsAuthenticationFailed()
    {
        // Arrange
        var server = RunFakeServerAsync(async stream =>
        {
            await ReadHttpRequestAsync(stream);
            await WriteAsciiAsync(
                stream,
                "HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\n\r\n"
            );
        });

        // Act
        var result = await TestAsync(ProxyType.Http, "alice", "wrong");

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.AuthenticationFailed);
    }

    [Test]
    public async Task TestAsync_HttpProxyAnswers407WithoutCredentials_ReturnsAuthenticationRequired()
    {
        // Arrange
        var server = RunFakeServerAsync(async stream =>
        {
            await ReadHttpRequestAsync(stream);
            await WriteAsciiAsync(
                stream,
                "HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\n\r\n"
            );
        });

        // Act
        var result = await TestAsync(ProxyType.Http, username: null, password: null);

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.AuthenticationRequired);
    }

    [Test]
    public async Task TestAsync_HttpProxyAnswers502_ReturnsSuccessAndSendsConnectWithCredentials()
    {
        // Arrange
        var request = string.Empty;
        var server = RunFakeServerAsync(async stream =>
        {
            request = await ReadHttpRequestAsync(stream);
            await WriteAsciiAsync(stream, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n");
        });

        // Act
        var result = await TestAsync(ProxyType.Http, "alice", "secret");

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.Success);
        request.ShouldStartWith(
            $"CONNECT {ProxyServerConnectionTester.HttpConnectTarget} HTTP/1.1\r\n"
        );
        request.ShouldContain($"Host: {ProxyServerConnectionTester.HttpConnectTarget}\r\n");
        request.ShouldContain(
            $"Proxy-Authorization: Basic {Convert.ToBase64String("alice:secret"u8.ToArray())}\r\n"
        );
    }

    [Test]
    public async Task TestAsync_HttpProxyWithoutCredentials_SendsNoProxyAuthorizationHeader()
    {
        // Arrange
        var request = string.Empty;
        var server = RunFakeServerAsync(async stream =>
        {
            request = await ReadHttpRequestAsync(stream);
            await WriteAsciiAsync(stream, "HTTP/1.1 200 Connection established\r\n\r\n");
        });

        // Act
        var result = await TestAsync(ProxyType.Http, username: null, password: null);

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.Success);
        request.ShouldNotContain("Proxy-Authorization");
    }

    [Test]
    public async Task TestAsync_Socks5ServerTestedAsHttpProxy_ReturnsWrongProtocol()
    {
        // Arrange
        var server = RunFakeServerAsync(async stream =>
        {
            await ReadBytesAsync(stream, 2);
            await stream.WriteAsync(new byte[] { 0x05, 0xFF });
        });

        // Act
        var result = await TestAsync(ProxyType.Http, "alice", "secret");

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.WrongProtocol);
    }

    [Test]
    public async Task TestAsync_ServerClosesConnectionWithoutAnswerToHttpRequest_ReturnsWrongProtocol()
    {
        // Arrange
        var server = RunFakeServerAsync(async stream => await ReadBytesAsync(stream, 2));

        // Act
        var result = await TestAsync(ProxyType.Http, username: null, password: null);

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.WrongProtocol);
    }

    [Test]
    public async Task TestAsync_HttpProxyTestedAsSocks5Proxy_ReturnsWrongProtocol()
    {
        // Arrange
        var server = RunFakeServerAsync(async stream =>
        {
            await ReadBytesAsync(stream, 4);
            await WriteAsciiAsync(stream, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n");
        });

        // Act
        var result = await TestAsync(ProxyType.Socks5, "alice", "secret");

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.WrongProtocol);
    }

    [Test]
    public async Task TestAsync_ServerClosesConnectionDuringSocks5Handshake_ReturnsWrongProtocol()
    {
        // Arrange
        var server = RunFakeServerAsync(async stream => await ReadBytesAsync(stream, 3));

        // Act
        var result = await TestAsync(ProxyType.Socks5, username: null, password: null);

        // Assert
        await server;
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.WrongProtocol);
    }

    [Test]
    public async Task TestAsync_ClosedPort_ReturnsNotReachable()
    {
        // Arrange
        fakeServer.Stop();

        // Act
        var result = await TestAsync(ProxyType.Http, username: null, password: null);

        // Assert
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.NotReachable);
        result.TechnicalDetail.ShouldNotBeNullOrEmpty();
    }

    private async Task<ProxyServerConnectionTestResult> TestAsync(
        ProxyType proxyType,
        string? username,
        string? password
    )
    {
        using var timeoutSource = new CancellationTokenSource(TestTimeout);

        return await tester.TestAsync(
            new ProxyServerConnectionTestRequest(
                proxyType,
                "127.0.0.1",
                fakeServerPort,
                username,
                password
            ),
            timeoutSource.Token
        );
    }

    private async Task RunFakeServerAsync(Func<NetworkStream, Task> handleConnection)
    {
        using var timeoutSource = new CancellationTokenSource(TestTimeout);
        using var client = await fakeServer.AcceptTcpClientAsync(timeoutSource.Token);
        await using var stream = client.GetStream();
        await handleConnection(stream);
    }

    private static async Task<byte[]> ReadBytesAsync(NetworkStream stream, int count)
    {
        var buffer = new byte[count];
        await stream.ReadExactlyAsync(buffer);

        return buffer;
    }

    private static async Task<string> ReadHttpRequestAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        var request = new StringBuilder();

        while (!request.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            var readByteCount = await stream.ReadAsync(buffer);
            request.Append(Encoding.ASCII.GetString(buffer, 0, readByteCount));
        }

        return request.ToString();
    }

    private static async Task WriteAsciiAsync(NetworkStream stream, string text)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(text));
    }
}
