using System.Text;
using Bearcat.Abstractions.NfoDatabase;
using NfoReleaseNfo = Bearcat.Abstractions.NfoDatabase.ReleaseNfo;

namespace Bearcat.Domain.UseCases.ManageReleases;

public enum ReleaseNfoFileSaveResult
{
    Saved,
    AlreadyExists,
    ReleaseFolderMissing,
}

public static class ReleaseNfoService
{
    public static async Task<bool> HasLocalNfoAsync(
        string? releaseFolderPath,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(releaseFolderPath))
        {
            return false;
        }

        return await Task.Run(
            () => !string.IsNullOrWhiteSpace(FindNfoPath(releaseFolderPath)),
            cancellationToken
        );
    }

    public static async Task<NfoReleaseNfo?> GetLocalNfoAsync(string? releaseFolderPath)
    {
        if (string.IsNullOrWhiteSpace(releaseFolderPath))
        {
            return null;
        }

        try
        {
            var nfoPath = await Task.Run(() => FindNfoPath(releaseFolderPath));
            if (string.IsNullOrWhiteSpace(nfoPath))
            {
                return null;
            }

            var content = NfoTextDecoder.Decode(await File.ReadAllBytesAsync(nfoPath));
            return new NfoReleaseNfo(Path.GetFileName(nfoPath), content);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or NotSupportedException
                        or ArgumentException
            )
        {
            return null;
        }
    }

    public static async Task<ReleaseNfoFileSaveResult> SaveNfoFileAsync(
        string? releaseFolderPath,
        string fileName,
        string releaseName,
        string content,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(releaseFolderPath) || !Directory.Exists(releaseFolderPath))
        {
            return ReleaseNfoFileSaveResult.ReleaseFolderMissing;
        }

        if (!string.IsNullOrWhiteSpace(FindNfoPath(releaseFolderPath)))
        {
            return ReleaseNfoFileSaveResult.AlreadyExists;
        }

        var safeFileName = GetSafeNfoFileName(fileName, releaseName);
        var nfoPath = Path.Combine(releaseFolderPath, safeFileName);

        await File.WriteAllTextAsync(nfoPath, content, Encoding.UTF8, cancellationToken);
        return ReleaseNfoFileSaveResult.Saved;
    }

    private static string? FindNfoPath(string releaseFolderPath)
    {
        if (!Directory.Exists(releaseFolderPath))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(releaseFolderPath, "*", SearchOption.TopDirectoryOnly)
            .Where(filePath =>
                string.Equals(
                    Path.GetExtension(filePath),
                    ".nfo",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string GetSafeNfoFileName(string fileName, string releaseName)
    {
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = Path.GetFileName(releaseName);
        }

        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "release";
        }

        return string.Equals(
            Path.GetExtension(safeFileName),
            ".nfo",
            StringComparison.OrdinalIgnoreCase
        )
            ? safeFileName
            : $"{safeFileName}.nfo";
    }
}
