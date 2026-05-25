namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseNfoService
{
    public async Task<string?> GetNfoContentAsync(string? releaseFolderPath)
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

            return await File.ReadAllTextAsync(nfoPath);
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
}
