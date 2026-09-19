using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using DomainReleaseNfo = Bearcat.Domain.Entities.ReleaseNfo;

namespace Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;

public class ReleaseNfoResolver(
    INfoDatabaseFactory nfoDatabaseFactory,
    ILogger<ReleaseNfoResolver> logger
)
{
    public async Task<bool> TryResolveAndAttachNfoAsync(
        Release release,
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        CancellationToken cancellationToken
    )
    {
        if (release.ReleaseNfo is not null)
        {
            return false;
        }

        var localNfo = await ReleaseNfoService.GetLocalNfoAsync(release.ReleaseFolderPath);

        if (localNfo is not null)
        {
            AttachNfo(release, localNfo.FileName, localNfo.Content);
            return true;
        }

        return await TryResolveNfoAsync(release, registrations, cancellationToken);
    }

    private async Task<bool> TryResolveNfoAsync(
        Release release,
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        CancellationToken cancellationToken
    )
    {
        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var nfoDatabase = nfoDatabaseFactory.Get(registration.NfoDatabaseClassName);

                if (nfoDatabase is not INfoProvider nfoProvider)
                {
                    continue;
                }

                var config = nfoDatabase.DeserializeConfig(registration.SerializedConfig);
                var nfo = await nfoProvider.GetReleaseNfoAsync(
                    config: config,
                    dirname: release.ReleaseInfo?.ReleaseName ?? release.Name,
                    cancellationToken: cancellationToken
                );

                if (nfo is null)
                {
                    continue;
                }

                AttachNfo(release, nfo.FileName, nfo.Content);

                logger.LogInformation(
                    "Resolved NFO for release {ReleaseName} using {NfoDatabase}",
                    release.Name,
                    registration.NfoDatabaseClassName
                );

                await SaveNfoFileToDiskAsync(
                    release: release,
                    fileName: nfo.FileName,
                    content: nfo.Content,
                    cancellationToken: cancellationToken
                );

                return true;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to resolve NFO for release {ReleaseName} using {NfoDatabase}",
                    release.Name,
                    registration.NfoDatabaseClassName
                );
            }
        }

        return false;
    }

    private static void AttachNfo(Release release, string fileName, string content)
    {
        release.ReleaseNfo = new DomainReleaseNfo { FileName = fileName, Content = content };
        ReleaseExternalIdentifierService.SyncImdbIds(
            release: release,
            source: ExternalIdentifierSource.Nfo,
            values: [content]
        );
        ReleaseExternalIdentifierService.SyncSteamAppIds(
            release: release,
            source: ExternalIdentifierSource.Nfo,
            values: [content]
        );
    }

    private async Task SaveNfoFileToDiskAsync(
        Release release,
        string fileName,
        string content,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await ReleaseNfoService.SaveNfoFileAsync(
                releaseFolderPath: release.ReleaseFolderPath,
                fileName: fileName,
                releaseName: release.Name,
                content: content,
                cancellationToken: cancellationToken
            );

            if (result is ReleaseNfoFileSaveResult.Saved)
            {
                logger.LogInformation("Saved NFO file for release {ReleaseName}", release.Name);
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            logger.LogWarning(
                exception,
                "Failed to save NFO file for release {ReleaseName}",
                release.Name
            );
        }
    }
}
