using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Microsoft.Extensions.Options;

namespace Bearcat.Domain.UseCases.FirstStartSetup.ArchiveDefaults;

public class FirstStartArchiveDefaultsService(
    IHosterConfigurationReadRepository hosterConfigurationReadRepository,
    IArchiverFactory archiverFactory,
    IFileSystemService fileSystemService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig
)
{
    private const string SuggestedArchiveOutputFolderName = "bearcat-archives";
    private const string RarArchiveFileExtension = ".rar";
    private const int ArchiveFileSizeMbWithoutHosterFileSizeLimit = 0;

    public async Task<FirstStartArchiveDefaults> GetArchiveDefaultsAsync(
        List<int> hosterRegistrationIds,
        CancellationToken cancellationToken = default
    )
    {
        var firstExistingWritableWorkingDirectory = workingDirectoriesConfig
            .Value.GetWorkingDirectories()
            .First(workingDirectory =>
                fileSystemService.DirectoryExists(workingDirectory)
                && fileSystemService.IsDirectoryWritable(workingDirectory)
            );

        var suggestedArchiveOutputFolderPath = Path.Combine(
            firstExistingWritableWorkingDirectory,
            SuggestedArchiveOutputFolderName
        );

        return new FirstStartArchiveDefaults(
            GetRarArchiverName(),
            await GetArchiveFileSizeMbAsync(hosterRegistrationIds, cancellationToken),
            suggestedArchiveOutputFolderPath,
            fileSystemService.DirectoryExists(suggestedArchiveOutputFolderPath)
        );
    }

    private string GetRarArchiverName()
    {
        return archiverFactory
            .GetArchivers()
            .Single(archiver => archiver.FileExtension == RarArchiveFileExtension)
            .ClassName;
    }

    private async Task<int> GetArchiveFileSizeMbAsync(
        List<int> hosterRegistrationIds,
        CancellationToken cancellationToken
    )
    {
        var hosterRegistrations = await hosterConfigurationReadRepository.GetAllRegistrationsAsync(
            cancellationToken
        );

        var smallestMaxFileSizeMb = hosterRegistrations
            .Where(hosterRegistration => hosterRegistrationIds.Contains(hosterRegistration.Id))
            .Min(hosterRegistration => hosterRegistration.MaxFileSizeMb);

        return smallestMaxFileSizeMb ?? ArchiveFileSizeMbWithoutHosterFileSizeLimit;
    }
}
