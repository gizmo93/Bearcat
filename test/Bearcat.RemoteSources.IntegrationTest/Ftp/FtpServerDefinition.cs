using Bearcat.RemoteSources.Ftp;

namespace Bearcat.RemoteSources.IntegrationTest.Ftp;

public sealed record FtpServerDefinition(
    string DockerDirectory,
    string? VsftpdMode,
    int ControlPort,
    string HomeDirectory,
    string Username,
    string Password,
    FtpEncryption Encryption
)
{
    private const string VsftpdDirectory = "Docker/Vsftpd";

    private const string VsftpdHomeDirectory = "/srv/ftp";

    private const string VsftpdUsername = "ftpuser";

    private const string VsftpdPassword = "ftppass";

    public static FtpServerDefinition For(FtpTestServer server)
    {
        return server switch
        {
            FtpTestServer.VsftpdPlain => CreateVsftpd("plain", 21, FtpEncryption.None),
            FtpTestServer.VsftpdExplicitTls => CreateVsftpd("explicit", 21, FtpEncryption.Explicit),
            FtpTestServer.VsftpdExplicitTlsWithSessionReuse => CreateVsftpd(
                "explicit_reuse",
                21,
                FtpEncryption.Explicit
            ),
            FtpTestServer.VsftpdExplicitTlsWithoutSessionResumption => CreateVsftpd(
                "explicit_no_resumption",
                21,
                FtpEncryption.Explicit
            ),
            FtpTestServer.VsftpdImplicitTls => CreateVsftpd(
                "implicit",
                990,
                FtpEncryption.Implicit
            ),
            FtpTestServer.Glftpd => new FtpServerDefinition(
                DockerDirectory: "Docker/Glftpd",
                VsftpdMode: null,
                ControlPort: 21,
                HomeDirectory: "/glftpd/site",
                Username: "glftpd",
                Password: "glftpd",
                Encryption: FtpEncryption.Explicit
            ),
            _ => throw new InvalidOperationException($"Unknown FTP test server {server}"),
        };
    }

    private static FtpServerDefinition CreateVsftpd(
        string mode,
        int controlPort,
        FtpEncryption encryption
    )
    {
        return new FtpServerDefinition(
            DockerDirectory: VsftpdDirectory,
            VsftpdMode: mode,
            ControlPort: controlPort,
            HomeDirectory: VsftpdHomeDirectory,
            Username: VsftpdUsername,
            Password: VsftpdPassword,
            Encryption: encryption
        );
    }
}
