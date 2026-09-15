using Bearcat.Abstractions.MediaMetadataDatabase;

namespace Bearcat.Domain.Shared.MediaMetadataResolution;

public record ResolvedMediaMetadata(string DatabaseClassName, MediaMetadata Metadata);
