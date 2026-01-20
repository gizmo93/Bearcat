namespace BearCat.Core.Domain.Abstractions.Archiver;

public interface IArchiverFactory
{
    IArchiver GetByName(string name);
    IReadOnlyList<ArchiverDto> GetArchivers();
}
