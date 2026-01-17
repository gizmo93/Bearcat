using BearCat.Core.Domain.UseCases.ManageUploads;

namespace BearCat.Core.Domain.Abstractions.Hoster;

public interface IHosterFactory
{
    IHoster GetByName(string name);
    IReadOnlyList<HosterReadModel> GetHosterReadModels();
    IReadOnlyDictionary<string, IHoster> GetHostersByName();
}
