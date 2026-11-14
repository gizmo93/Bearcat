using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.UseCases.ManageReleases.Parsers;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.MetadataMatchCorrection;

public record MetadataMatchCorrectionPreview(
    MediaDatabaseReference Reference,
    string DatabaseClassName,
    MediaMetadata Metadata
)
{
    public string GetCanonicalReferenceValue()
    {
        return Reference.Type switch
        {
            ExternalIdentifierType.Tmdb => Metadata.TmdbId
                ?? throw new InvalidOperationException(
                    "The media database did not report a TMDB id for a TMDB reference."
                ),
            ExternalIdentifierType.Tvdb => Metadata.TvdbId
                ?? throw new InvalidOperationException(
                    "The media database did not report a TVDB id for a TVDB reference."
                ),
            _ => Reference.Value,
        };
    }
}
