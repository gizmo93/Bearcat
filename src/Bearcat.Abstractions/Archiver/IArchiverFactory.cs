namespace Bearcat.Abstractions.Archiver;

public interface IArchiverFactory
{
    IArchiver GetByName(string name);
    IReadOnlyList<ArchiverDto> GetArchivers();
    IReadOnlyList<IArchiveExtractor> GetArchiveExtractors();
    IArchiveExtractor GetArchiveExtractorByName(string name);
}
