namespace Bearcat.RemoteSources.IntegrationTest.Ftp;

public enum FtpTestServer
{
    VsftpdPlain = 1,
    VsftpdExplicitTls = 2,
    VsftpdExplicitTlsWithSessionReuse = 3,
    VsftpdImplicitTls = 5,
    Glftpd = 6,
}
