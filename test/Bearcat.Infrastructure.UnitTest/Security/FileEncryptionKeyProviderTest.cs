using System.Security.Cryptography;
using Bearcat.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bearcat.Infrastructure.UnitTest.Security;

public class FileEncryptionKeyProviderTest
{
    private const string KeyFilePrefix = "bearcat-key-v1:";

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), $"bearcat-key-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void GetKey_BeforeInitialize_Throws()
    {
        // Arrange
        var provider = CreateProvider(KeyPathIn("bearcat.key"));

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => provider.GetKey());
    }

    [Test]
    public async Task InitializeAsync_NoExistingFile_CreatesKeyFileAndReturns32Bytes()
    {
        // Arrange
        var keyPath = KeyPathIn("nested", "bearcat.key");
        var provider = CreateProvider(keyPath);

        // Act
        await provider.InitializeAsync();

        // Assert
        provider.GetKey().Length.ShouldBe(32);
        File.Exists(keyPath).ShouldBeTrue();
        (await File.ReadAllTextAsync(keyPath)).Trim().ShouldStartWith(KeyFilePrefix);
    }

    [Test]
    public async Task InitializeAsync_ExistingPrefixedKeyFile_LoadsKey()
    {
        // Arrange
        var keyPath = KeyPathIn("bearcat.key");
        var expectedKey = new byte[32];
        RandomNumberGenerator.Fill(expectedKey);
        await File.WriteAllTextAsync(
            keyPath,
            $"{KeyFilePrefix}{Convert.ToBase64String(expectedKey)}{Environment.NewLine}"
        );
        var provider = CreateProvider(keyPath);

        // Act
        await provider.InitializeAsync();

        // Assert
        provider.GetKey().ShouldBe(expectedKey);
    }

    [Test]
    public async Task InitializeAsync_ExistingKeyFileWithoutPrefix_LoadsKey()
    {
        // Arrange
        var keyPath = KeyPathIn("bearcat.key");
        var expectedKey = new byte[32];
        RandomNumberGenerator.Fill(expectedKey);
        await File.WriteAllTextAsync(keyPath, Convert.ToBase64String(expectedKey));
        var provider = CreateProvider(keyPath);

        // Act
        await provider.InitializeAsync();

        // Assert
        provider.GetKey().ShouldBe(expectedKey);
    }

    [Test]
    public async Task InitializeAsync_KeyFileWithWrongLength_Throws()
    {
        // Arrange
        var keyPath = KeyPathIn("bearcat.key");
        await File.WriteAllTextAsync(
            keyPath,
            $"{KeyFilePrefix}{Convert.ToBase64String(new byte[16])}"
        );
        var provider = CreateProvider(keyPath);

        // Act / Assert
        await Should.ThrowAsync<InvalidOperationException>(() => provider.InitializeAsync());
    }

    [Test]
    public async Task InitializeAsync_CalledTwice_DoesNotRegenerateKey()
    {
        // Arrange
        var keyPath = KeyPathIn("bearcat.key");
        var provider = CreateProvider(keyPath);
        await provider.InitializeAsync();
        var originalKey = provider.GetKey();

        File.Delete(keyPath);

        // Act
        await provider.InitializeAsync();

        // Assert
        provider.GetKey().ShouldBe(originalKey);
        File.Exists(keyPath).ShouldBeFalse();
    }

    [Test]
    public void KeyPath_ConfiguredMasterKeyPath_IsResolvedToFullPath()
    {
        // Arrange
        var provider = CreateProvider("relative/key/path/bearcat.key");

        // Act / Assert
        provider.KeyPath.ShouldBe(Path.GetFullPath("relative/key/path/bearcat.key"));
    }

    [Test]
    public void KeyPath_ConfiguredDataDirectory_CombinesWithKeyFileName()
    {
        // Arrange
        var provider = new FileEncryptionKeyProvider(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Bearcat:DataDirectory"] = tempDirectory }
                )
                .Build(),
            NullLogger<FileEncryptionKeyProvider>.Instance
        );

        // Act / Assert
        provider.KeyPath.ShouldBe(Path.GetFullPath(Path.Combine(tempDirectory, "bearcat.key")));
    }

    private string KeyPathIn(params string[] segments)
    {
        return Path.Combine([tempDirectory, .. segments]);
    }

    private static FileEncryptionKeyProvider CreateProvider(string masterKeyPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Security:MasterKeyPath"] = masterKeyPath }
            )
            .Build();

        return new FileEncryptionKeyProvider(
            configuration,
            NullLogger<FileEncryptionKeyProvider>.Instance
        );
    }
}
