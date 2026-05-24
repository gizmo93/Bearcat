namespace Bearcat.Infrastructure.Security;

public sealed class NoOpSecretProtector : ISecretProtector
{
    public static NoOpSecretProtector Instance { get; } = new();

    public string Protect(string plaintext) => plaintext;

    public string Unprotect(string protectedValue) => protectedValue;

    public bool IsProtected(string value) => false;
}
