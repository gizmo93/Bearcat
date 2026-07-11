namespace Bearcat.Abstractions.MediaMetadataDatabase;

public interface IMediaMetadataDatabaseFactory
{
    IReadOnlyList<MediaMetadataDatabaseDto> GetDatabases();

    IMediaMetadataDatabase Get(string className);

    IReadOnlyDictionary<string, IMediaMetadataDatabase> GetByClassName();
}
