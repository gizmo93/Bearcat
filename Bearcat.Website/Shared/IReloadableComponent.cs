namespace Bearcat.Website.Shared;

public interface IReloadableComponent
{
    Task ReloadAsync();
}
