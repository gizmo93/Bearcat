using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates;

public static class UnmanagedArchiveConfigTemplates
{
    private const string UnmanagedArchiveConfigTemplateName = "Unmanaged archives";
    private const string UnmanagedArchiveFilesBasePath = "Release folder";
    private const string UnmanagedArchiverName = "Unmanaged";

    public static ArchiveConfigTemplate Create()
    {
        return new ArchiveConfigTemplate
        {
            Name = UnmanagedArchiveConfigTemplateName,
            ArchiveFilesBasePath = UnmanagedArchiveFilesBasePath,
            ArchiverName = UnmanagedArchiverName,
            ArchivePassword = null,
            ArchiveFileSizeMb = 0,
            UseReleaseNameAsArchiveName = false,
        };
    }

    public static void AddIfMissing(ReleaseTemplate releaseTemplate)
    {
        if (releaseTemplate.ReleaseType is not ReleaseType.Unmanaged)
        {
            return;
        }

        if (releaseTemplate.ArchiveConfigTemplates.Count > 0)
        {
            return;
        }

        releaseTemplate.ArchiveConfigTemplates.Add(Create());
    }

    public static ArchiveConfigTemplate? GetForUnmanagedReleaseTemplateOrNull(
        ReleaseTemplate releaseTemplate
    )
    {
        if (releaseTemplate.ReleaseType is ReleaseType.Managed)
        {
            return null;
        }

        AddIfMissing(releaseTemplate);
        return releaseTemplate.ArchiveConfigTemplates.Single();
    }
}
