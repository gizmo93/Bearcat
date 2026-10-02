using System.Security.Cryptography;
using Bearcat.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bearcat.Infrastructure.Security;

public sealed class FileEncryptionKeyProvider(
    IConfiguration configuration,
    ILogger<FileEncryptionKeyProvider> logger
) : IEncryptionKeyProvider
{
    private const int KeySizeInBytes = 32;
    private const string KeyFileName = "bearcat.key";
    private const string KeyFilePrefix = "bearcat-key-v1:";

    private byte[]? key;

    public string KeyPath => ResolveKeyPath(configuration);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        key ??= await LoadOrCreateKeyAsync(KeyPath, logger, cancellationToken);
    }

    public byte[] GetKey()
    {
        return key
            ?? throw new InvalidOperationException(
                "Bearcat encryption key has not been initialized."
            );
    }

    private static async Task<byte[]> LoadOrCreateKeyAsync(
        string keyPath,
        ILogger<FileEncryptionKeyProvider> logger,
        CancellationToken cancellationToken = default
    )
    {
        if (File.Exists(keyPath))
        {
            return await ReadKeyAsync(keyPath, cancellationToken);
        }

        var directory = Path.GetDirectoryName(keyPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var key = RandomNumberGenerator.GetBytes(KeySizeInBytes);
        var keyFileContent = $"{KeyFilePrefix}{Convert.ToBase64String(key)}{Environment.NewLine}";
        await File.WriteAllTextAsync(keyPath, keyFileContent, cancellationToken);
        TryRestrictFilePermissions(keyPath);

        logger.LogInformation("Created Bearcat encryption key at {KeyPath}", keyPath);

        return key;
    }

    private static async Task<byte[]> ReadKeyAsync(
        string keyPath,
        CancellationToken cancellationToken = default
    )
    {
        var content = (await File.ReadAllTextAsync(keyPath, cancellationToken)).Trim();
        if (content.StartsWith(KeyFilePrefix, StringComparison.Ordinal))
        {
            content = content[KeyFilePrefix.Length..];
        }

        var key = Convert.FromBase64String(content);
        if (key.Length != KeySizeInBytes)
        {
            throw new InvalidOperationException(
                $"Bearcat encryption key at {keyPath} must contain {KeySizeInBytes} bytes."
            );
        }

        return key;
    }

    private static string ResolveKeyPath(IConfiguration configuration)
    {
        var configuredKeyPath =
            Environment.GetEnvironmentVariable("BEARCAT_MASTER_KEY_FILE")
            ?? configuration["Security:MasterKeyPath"];

        if (!string.IsNullOrWhiteSpace(configuredKeyPath))
        {
            return Path.GetFullPath(configuredKeyPath);
        }

        return Path.Combine(BearcatDataDirectory.Resolve(configuration), KeyFileName);
    }

    private static void TryRestrictFilePermissions(string keyPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
            // Some mounted file systems do not support Unix permissions.
        }
    }
}
