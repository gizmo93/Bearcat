namespace BearCat.Core.Hosters;

public class HosterService(IEnumerable<IHoster> hosters)
{
    public IReadOnlyList<string> GetAllHosterNamesAsync()
    {
        return hosters.Select(h => h.Name).ToList();
    }
}