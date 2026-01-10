using BearCat.Core.Domain.Abstractions.Hoster;

namespace BearCat.Core.Domain.UseCases.ManageUploads;

public class HosterInstanceService(IEnumerable<IHoster> hosters)
{
    public IHoster GetByFullClassName(string fullClassName)
    {
        return hosters.First(h => h.GetType().FullName == fullClassName);
    }
}
