using System.Security.Cryptography;
using Bearcat.Infrastructure.Security;
using Shouldly;

namespace Bearcat.Infrastructure.UnitTest.Security;

public class AesGcmSecretProtectorTest
{
    private const string Prefix = "bearcat:enc:v1:";

    private AesGcmSecretProtector protector = null!;

    [SetUp]
    public void SetUp()
    {
        protector = new AesGcmSecretProtector(new FixedKeyProvider());
    }

    [Test]
    public void ProtectThenUnprotect_RoundTripsPlaintext()
    {
        // Act
        var protectedValue = protector.Protect("super-secret-token");
        var unprotected = protector.Unprotect(protectedValue);

        // Assert
        unprotected.ShouldBe("super-secret-token");
    }

    [Test]
    public void Protect_ProducesPrefixedBase64Payload()
    {
        // Act
        var protectedValue = protector.Protect("value");

        // Assert
        protectedValue.ShouldStartWith(Prefix);
        Should.NotThrow(() => Convert.FromBase64String(protectedValue[Prefix.Length..]));
    }

    [Test]
    public void Protect_AlreadyProtectedValue_ReturnsValueUnchanged()
    {
        // Arrange
        var protectedValue = protector.Protect("value");

        // Act
        var result = protector.Protect(protectedValue);

        // Assert
        result.ShouldBe(protectedValue);
    }

    [Test]
    public void Protect_SamePlaintextTwice_ProducesDifferentCiphertext()
    {
        // Act
        var first = protector.Protect("value");
        var second = protector.Protect("value");

        // Assert
        first.ShouldNotBe(second);
        protector.Unprotect(first).ShouldBe("value");
        protector.Unprotect(second).ShouldBe("value");
    }

    [Test]
    public void Unprotect_UnprotectedValue_ReturnsValueUnchanged()
    {
        // Act
        var result = protector.Unprotect("plain-value");

        // Assert
        result.ShouldBe("plain-value");
    }

    [Test]
    [TestCase("bearcat:enc:v1:abc", true)]
    [TestCase("plain", false)]
    [TestCase("", false)]
    public void IsProtected_DetectsPrefix(string value, bool expected)
    {
        // Act / Assert
        protector.IsProtected(value).ShouldBe(expected);
    }

    [Test]
    public void Unprotect_PayloadShorterThanNonceAndTag_Throws()
    {
        // Arrange
        var tooShort = Prefix + Convert.ToBase64String(new byte[10]);

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => protector.Unprotect(tooShort));
    }

    [Test]
    public void Unprotect_TamperedPayload_ThrowsCryptographicException()
    {
        // Arrange
        var tampered = Prefix + Convert.ToBase64String(new byte[30]);

        // Act / Assert
        Should.Throw<CryptographicException>(() => protector.Unprotect(tampered));
    }

    [Test]
    public void Unprotect_PayloadEncryptedWithDifferentKey_ThrowsCryptographicException()
    {
        // Arrange
        var protectedValue = protector.Protect("value");
        var otherProtector = new AesGcmSecretProtector(new FixedKeyProvider(fillValue: 7));

        // Act / Assert
        Should.Throw<CryptographicException>(() => otherProtector.Unprotect(protectedValue));
    }

    private sealed class FixedKeyProvider(byte fillValue = 1) : IEncryptionKeyProvider
    {
        private readonly byte[] key = Enumerable.Repeat(fillValue, 32).ToArray();

        public string KeyPath => "in-memory";

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public byte[] GetKey() => key;
    }
}
