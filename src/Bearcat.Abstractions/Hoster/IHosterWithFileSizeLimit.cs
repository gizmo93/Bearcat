namespace Bearcat.Abstractions.Hoster;

public interface IHosterWithFileSizeLimit : IHoster
{
    int MaxFileSizeMb { get; }
}
