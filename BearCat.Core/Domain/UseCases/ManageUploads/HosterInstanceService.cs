using BearCat.Core.Domain.Abstractions.Hoster;

namespace BearCat.Core.Domain.UseCases.ManageUploads;

public class HosterInstanceService(IEnumerable<IHoster> hosters)
{
    public IHoster GetByFullClassName(string fullClassName)
    {
        return hosters.First(h => h.GetType().FullName == fullClassName);
    }

    public IReadOnlyList<HosterReadModel> GetHosterReadModels()
    {
        return hosters
            .Select(h => new HosterReadModel(
                Name: h.Name,
                FullClassName: h.GetType().FullName!,
                ConfigurationKeys: h.ConfigurationKeys))
            .ToList();
    }
}
