using Bearcat.Abstractions;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.QualityGate;

public sealed class QualityCheckContext(Release release, IFileSystemService fileSystemService)
{
    private IReadOnlyList<string>? files;
    private long? totalBytes;

    public Release Release => release;

    public IReadOnlyList<string> Files => files ??= ReadFiles();

    public long TotalBytes =>
        totalBytes ??= release.ReleaseFolderPath is null
            ? 0
            : fileSystemService.GetFolderContentFingerprint(release.ReleaseFolderPath).TotalBytes;

    private List<string> ReadFiles()
    {
        return
            release.ReleaseFolderPath is null
            || !fileSystemService.DirectoryExists(release.ReleaseFolderPath)
            ? []
            : fileSystemService.GetFilesInPath(release.ReleaseFolderPath, recursive: true);
    }
}
