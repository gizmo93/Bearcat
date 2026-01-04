using BearCat.Core.Domain.Abstractions;
using BearCat.Core.Domain.Abstractions.Hoster;

namespace BearCat.Core.Infrastructure.Hosters;

public class HosterService(IEnumerable<IHoster> hosters)
{
    public IReadOnlyList<string> GetAllHosterNames()
    {
        return hosters.Select(h => h.Name).ToList();
    }
}
