namespace Bearcat.Abstractions.Hoster;

public interface IHosterConfig
{
    public IReadOnlyDictionary<string, string> ToDictionary();
}
