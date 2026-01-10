using BearCat.Core.Domain.Abstractions.Archiver;

namespace BearCat.Core.Domain.UseCases.ManageArchives;

public class ArchiverInstanceService(IEnumerable<IArchiver> archivers)
{
    public IArchiver GetByFullClassName(string fullClassName)
    {
        return archivers.First(a => a.GetType().FullName == fullClassName);
    }
}
