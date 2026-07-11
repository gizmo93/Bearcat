namespace Bearcat.Abstractions.MediaMetadataDatabase;

public interface IMediaMetadataDatabaseConfig
{
    IReadOnlyDictionary<string, string> ToDictionary();
}
