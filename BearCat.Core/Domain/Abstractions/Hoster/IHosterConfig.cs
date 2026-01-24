namespace BearCat.Core.Domain.Abstractions.Hoster;

public interface IHosterConfig
{
    public IReadOnlyDictionary<string, string> ToDictionary();
}
