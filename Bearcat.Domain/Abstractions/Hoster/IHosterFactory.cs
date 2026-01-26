using Bearcat.Domain.UseCases.ManageUploads;

namespace Bearcat.Domain.Abstractions.Hoster;

public interface IHosterFactory
{
    IHoster GetByName(string name);
    IReadOnlyList<HosterReadModel> GetHosterReadModels();
    IReadOnlyDictionary<string, IHoster> GetHostersByName();
}
