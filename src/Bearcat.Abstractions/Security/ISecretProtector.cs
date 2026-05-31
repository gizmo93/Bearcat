namespace Bearcat.Abstractions.Security;

public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);

    bool IsProtected(string value);
}
