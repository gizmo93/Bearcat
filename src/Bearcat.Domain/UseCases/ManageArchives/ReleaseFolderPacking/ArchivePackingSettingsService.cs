using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderPacking;

public class ArchivePackingSettingsService(
    IArchiveCreationRepository repository,
    ILogger<ArchivePackingSettingsService> logger,
    IApplicationConfigurationProvider configurationProvider
)
{
    public async Task<ArchivePackingSettings> GetPackingSettingsAsync(
        ArchiveConfig archiveConfig,
        IArchiver archiver,
        CancellationToken cancellationToken
    )
    {
        var strategy = configurationProvider.GetValue<ArchiveRepackagingConfiguration>(c =>
            c.Strategy
        );

        var incrementLastArchiveFileSize = await ShouldIncrementLastArchiveFileSizeAsync(
            strategy: strategy,
            archiveConfig: archiveConfig,
            archiver: archiver,
            cancellationToken: cancellationToken
        );

        return new ArchivePackingSettings(
            ArchiveFileSizeMb: await GetArchiveFileSizeMbAsync(
                archiveConfig: archiveConfig,
                incrementLastArchiveFileSize: incrementLastArchiveFileSize,
                cancellationToken: cancellationToken
            ),
            Options: CreateArchiveOptions(strategy, archiveConfig)
        );
    }

    private static ArchiveOptions CreateArchiveOptions(
        string? strategy,
        ArchiveConfig archiveConfig
    )
    {
        return strategy switch
        {
            ArchiveRepackagingStrategies.SolidCompression => new ArchiveOptions(
                UseCompression: true,
                UseSolidArchive: true,
                PackSourceFolderAsRootFolder: archiveConfig.PackReleaseFolderAsRootFolder
            ),
            _ => new ArchiveOptions(
                UseCompression: false,
                UseSolidArchive: false,
                PackSourceFolderAsRootFolder: archiveConfig.PackReleaseFolderAsRootFolder
            ),
        };
    }

    private async Task<bool> ShouldIncrementLastArchiveFileSizeAsync(
        string? strategy,
        ArchiveConfig archiveConfig,
        IArchiver archiver,
        CancellationToken cancellationToken
    )
    {
        switch (strategy)
        {
            case ArchiveRepackagingStrategies.NonceOnly:
            case ArchiveRepackagingStrategies.SolidCompression:
                return !archiveConfig.CreateNonceFile && !archiver.CanChangeHashInPlace;
            case ArchiveRepackagingStrategies.IncrementArchiveFileSize:
                return !await CanUseHashAppendStrategyAsync(
                    archiveConfig,
                    archiver,
                    cancellationToken
                );
            default:
                logger.LogWarning(
                    "Unknown archive repackaging strategy {Strategy}. Falling back to {DefaultStrategy}.",
                    strategy,
                    ArchiveRepackagingStrategies.IncrementArchiveFileSize
                );

                return !await CanUseHashAppendStrategyAsync(
                    archiveConfig,
                    archiver,
                    cancellationToken
                );
        }
    }

    private async Task<bool> CanUseHashAppendStrategyAsync(
        ArchiveConfig archiveConfig,
        IArchiver archiver,
        CancellationToken cancellationToken
    )
    {
        var lastArchiveHasFilesWithoutHash = await repository.LastArchiveHasFilesWithoutHashAsync(
            archiveConfigId: archiveConfig.Id,
            cancellationToken: cancellationToken
        );

        return archiver.CanChangeHashInPlace && !lastArchiveHasFilesWithoutHash;
    }

    private async Task<int> GetArchiveFileSizeMbAsync(
        ArchiveConfig archiveConfig,
        bool incrementLastArchiveFileSize,
        CancellationToken cancellationToken
    )
    {
        if (!incrementLastArchiveFileSize)
        {
            return archiveConfig.ArchiveFileSizeMb;
        }

        var lastArchiveFileSizeMb = await repository.GetLastArchiveFileSizeMbAsync(
            archiveConfigId: archiveConfig.Id,
            cancellationToken: cancellationToken
        );

        return lastArchiveFileSizeMb is null
            ? archiveConfig.ArchiveFileSizeMb
            : lastArchiveFileSizeMb.Value + 1;
    }
}
