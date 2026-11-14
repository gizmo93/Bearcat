using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.UseCases.ManageReleases.Parsers;

namespace Bearcat.Domain.UseCases.ManageReleases.MetadataMatchCorrection;

public record MetadataMatchCorrectionPreview(
    MediaDatabaseReference Reference,
    string DatabaseClassName,
    MediaMetadata Metadata
);
