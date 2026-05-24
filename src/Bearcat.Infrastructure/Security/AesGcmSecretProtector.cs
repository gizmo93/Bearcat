using System.Security.Cryptography;
using System.Text;

namespace Bearcat.Infrastructure.Security;

public sealed class AesGcmSecretProtector(IEncryptionKeyProvider keyProvider) : ISecretProtector
{
    private const string Prefix = "bearcat:enc:v1:";
    private const int NonceSizeInBytes = 12;
    private const int TagSizeInBytes = 16;

    public string Protect(string plaintext)
    {
        if (IsProtected(plaintext))
        {
            return plaintext;
        }

        var key = keyProvider.GetKey();
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeInBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeInBytes];

        using var aes = new AesGcm(key, TagSizeInBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var payload = new byte[NonceSizeInBytes + TagSizeInBytes + ciphertext.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSizeInBytes);
        ciphertext.CopyTo(payload, NonceSizeInBytes + TagSizeInBytes);

        return $"{Prefix}{Convert.ToBase64String(payload)}";
    }

    public string Unprotect(string protectedValue)
    {
        if (!IsProtected(protectedValue))
        {
            return protectedValue;
        }

        var payload = Convert.FromBase64String(protectedValue[Prefix.Length..]);
        if (payload.Length < NonceSizeInBytes + TagSizeInBytes)
        {
            throw new InvalidOperationException("Encrypted Bearcat secret payload is invalid.");
        }

        var nonce = payload[..NonceSizeInBytes];
        var tag = payload[NonceSizeInBytes..(NonceSizeInBytes + TagSizeInBytes)];
        var ciphertext = payload[(NonceSizeInBytes + TagSizeInBytes)..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(keyProvider.GetKey(), TagSizeInBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public bool IsProtected(string value)
    {
        return value.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
