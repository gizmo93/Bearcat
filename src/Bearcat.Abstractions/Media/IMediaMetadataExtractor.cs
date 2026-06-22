namespace Bearcat.Abstractions.Media;

public sealed record MediaProbeResult(string Json, string Text);

public interface IMediaMetadataExtractor
{
    Task<MediaProbeResult?> ExtractAsync(string filePath, CancellationToken cancellationToken);
}
