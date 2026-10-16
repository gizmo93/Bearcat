using Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;

namespace Bearcat.Domain.UseCases.FirstStartSetup.Sources;

public class ArchiveOutputFolderInsideSourceFolderValidator(
    IReleaseTemplateReadRepository releaseTemplateReadRepository
)
{
    public async Task EnsureNoArchiveOutputFolderIsInsideSourceFolderAsync(
        int releaseTemplateId,
        string sourceFolderPath,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await releaseTemplateReadRepository.GetDetailAsync(
            releaseTemplateId,
            cancellationToken
        );
        var fullSourceFolderPath = GetFullFolderPath(sourceFolderPath);

        var archiveOutputFolderInsideSourceFolder = releaseTemplate!
            .ArchiveConfigTemplates.Select(archiveConfigTemplate =>
                archiveConfigTemplate.ArchiveFilesBasePath
            )
            .FirstOrDefault(archiveOutputFolderPath =>
                FolderPathHelper.IsSameOrSubPath(
                    GetFullFolderPath(archiveOutputFolderPath),
                    fullSourceFolderPath
                )
            );

        if (archiveOutputFolderInsideSourceFolder is not null)
        {
            throw new ArchiveOutputFolderInsideSourceFolderException(
                archiveOutputFolderInsideSourceFolder,
                sourceFolderPath
            );
        }
    }

    private static string GetFullFolderPath(string folderPath)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath.Trim()));
    }
}
