using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseNameParsing;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseClassificationService(
    IReleaseClassificationRepository repository,
    TimeProvider timeProvider,
    ILogger<ReleaseClassificationService> logger
)
{
    private const int BatchSize = 100;

    public async Task<int> ProcessPendingClassificationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var totalClassified = 0;
        var seenReleaseIds = new HashSet<int>();
        var hasPendingReleases = true;

        while (hasPendingReleases)
        {
            var releases = await repository.GetReleasesNeedingClassificationAsync(
                count: BatchSize,
                parserVersion: ReleaseClassificationBuilder.CurrentParserVersion,
                excludedReleaseIds: seenReleaseIds,
                cancellationToken: cancellationToken
            );

            foreach (var release in releases)
            {
                cancellationToken.ThrowIfCancellationRequested();

                seenReleaseIds.Add(release.Id);
                Classify(release);
                totalClassified++;
            }

            await repository.SaveChangesAsync(cancellationToken);

            hasPendingReleases = releases.Count > 0;
        }

        return totalClassified;
    }

    public async Task<bool> ClassifyAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await repository.GetReleaseForClassificationAsync(
            releaseId,
            cancellationToken
        );

        if (release is null)
        {
            return false;
        }

        Classify(release);

        await repository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public void Classify(Release release)
    {
        var built = ReleaseClassificationBuilder.Build(release.Name, release.MediaFiles);
        built.ClassifiedAt = timeProvider.GetLocalNow();

        if (release.Classification is null)
        {
            built.ReleaseId = release.Id;
            release.Classification = built;
        }
        else
        {
            Apply(release.Classification, built);
        }

        logger.LogInformation(
            "Classified release {ReleaseName} as {Resolution} / {Language}",
            release.Name,
            built.Resolution,
            built.PrimaryLanguage ?? "unknown"
        );
    }

    private static void Apply(ReleaseClassification target, ReleaseClassification source)
    {
        target.Title = source.Title;
        target.Year = source.Year;
        target.Season = source.Season;
        target.Episode = source.Episode;
        target.EpisodeEnd = source.EpisodeEnd;
        target.ContentType = source.ContentType;
        target.Resolution = source.Resolution;
        target.ResolutionSource = source.ResolutionSource;
        target.Source = source.Source;
        target.SourceSource = source.SourceSource;
        target.ReleaseGroupToken = source.ReleaseGroupToken;
        target.PrimaryLanguage = source.PrimaryLanguage;
        target.LanguageSource = source.LanguageSource;
        target.IsMultiLanguage = source.IsMultiLanguage;
        target.ParserVersion = source.ParserVersion;
        target.ClassifiedAt = source.ClassifiedAt;
    }
}
