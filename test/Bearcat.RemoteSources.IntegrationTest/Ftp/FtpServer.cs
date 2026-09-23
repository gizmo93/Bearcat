using System.Globalization;
using Bearcat.RemoteSources.Ftp;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;

namespace Bearcat.RemoteSources.IntegrationTest.Ftp;

public sealed class FtpServer : IAsyncDisposable
{
    private const int PassivePortCount = 10;

    private readonly IContainer container;

    private readonly FtpServerDefinition definition;

    private FtpServer(IContainer container, FtpServerDefinition definition)
    {
        this.container = container;
        this.definition = definition;
    }

    public static async Task<FtpServer> StartAsync(IImage image, FtpServerDefinition definition)
    {
        var passivePortStart = FreePortRange.FindStart(PassivePortCount);
        var passivePortEnd = passivePortStart + PassivePortCount - 1;

        var builder = new ContainerBuilder(image)
            .WithEnvironment(
                "PASV_MIN_PORT",
                passivePortStart.ToString(CultureInfo.InvariantCulture)
            )
            .WithEnvironment("PASV_MAX_PORT", passivePortEnd.ToString(CultureInfo.InvariantCulture))
            .WithEnvironment("PASV_ADDRESS", "127.0.0.1")
            .WithPortBinding(definition.ControlPort, assignRandomHostPort: true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(definition.ControlPort)
                    .UntilExternalTcpPortIsAvailable(definition.ControlPort)
            );

        if (definition.VsftpdMode is not null)
        {
            builder = builder.WithEnvironment("FTP_MODE", definition.VsftpdMode);
        }

        for (var port = passivePortStart; port <= passivePortEnd; port++)
        {
            builder = builder.WithPortBinding(port, port);
        }

        foreach (var (relativePath, content) in FtpTestData.Files)
        {
            builder = builder.WithResourceMapping(
                content,
                $"{definition.HomeDirectory}/{relativePath}"
            );
        }

        var container = builder.Build();
        await container.StartAsync();

        return new FtpServer(container, definition);
    }

    public FtpRemoteSourceConfig CreateConfig(
        FtpTlsProvider tlsProvider,
        string? password = null,
        bool validateCertificate = false
    )
    {
        return new FtpRemoteSourceConfig
        {
            Host = container.Hostname,
            Port = container.GetMappedPublicPort(definition.ControlPort),
            Username = definition.Username,
            Password = password ?? definition.Password,
            Encryption = definition.Encryption,
            TlsProvider = tlsProvider,
            ValidateCertificate = validateCertificate,
        };
    }

    public ValueTask DisposeAsync()
    {
        return container.DisposeAsync();
    }
}
