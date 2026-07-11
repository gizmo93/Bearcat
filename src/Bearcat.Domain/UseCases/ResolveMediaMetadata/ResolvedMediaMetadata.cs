using Bearcat.Abstractions.MediaMetadataDatabase;

namespace Bearcat.Domain.UseCases.ResolveMediaMetadata;

public record ResolvedMediaMetadata(string DatabaseClassName, MediaMetadata Metadata);
