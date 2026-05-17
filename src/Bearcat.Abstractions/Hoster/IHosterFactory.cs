using Bearcat.Abstractions.Hoster.Dto;

namespace Bearcat.Abstractions.Hoster;

public interface IHosterFactory
{
    IHoster GetByName(string name);
    IReadOnlyList<HosterDto> GetHosterReadModels();
    IReadOnlyDictionary<string, IHoster> GetHostersByName();
}
