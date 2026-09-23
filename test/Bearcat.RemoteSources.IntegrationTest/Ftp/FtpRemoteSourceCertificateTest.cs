using System.Security.Authentication;
using Bearcat.RemoteSources.Ftp;
using Shouldly;

namespace Bearcat.RemoteSources.IntegrationTest.Ftp;

[TestFixture(FtpTestServer.VsftpdExplicitTls, FtpTlsProvider.System)]
[TestFixture(FtpTestServer.VsftpdExplicitTls, FtpTlsProvider.BouncyCastle)]
[TestFixture(FtpTestServer.VsftpdImplicitTls, FtpTlsProvider.System)]
[TestFixture(FtpTestServer.VsftpdImplicitTls, FtpTlsProvider.BouncyCastle)]
[TestFixture(FtpTestServer.Glftpd, FtpTlsProvider.System)]
[TestFixture(FtpTestServer.Glftpd, FtpTlsProvider.BouncyCastle)]
public class FtpRemoteSourceCertificateTest(FtpTestServer testServer, FtpTlsProvider tlsProvider)
{
    private readonly FtpRemoteSource remoteSource = new();
    private FtpServer server = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        server = await FtpTestEnvironment.GetServerAsync(testServer);
    }

    [Test]
    public async Task OpenSessionAsync_ValidateCertificateEnabled_RejectsSelfSignedCertificate()
    {
        // Arrange
        var config = server.CreateConfig(tlsProvider, validateCertificate: true);

        // Act
        var action = () => remoteSource.OpenSessionAsync(config, CancellationToken.None);

        // Assert
        await action.ShouldThrowAsync<AuthenticationException>();
    }

    [Test]
    public async Task OpenSessionAsync_ValidateCertificateDisabled_AcceptsSelfSignedCertificate()
    {
        // Arrange
        var config = server.CreateConfig(tlsProvider, validateCertificate: false);

        // Act
        await using var session = await remoteSource.OpenSessionAsync(
            config,
            CancellationToken.None
        );

        // Assert
        (await session.ListFoldersAsync("/", CancellationToken.None)).ShouldNotBeEmpty();
    }
}
