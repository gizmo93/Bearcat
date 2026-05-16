namespace Bearcat.Website.Blueprint.Shared;

public interface IReloadableComponent
{
    Task ReloadAsync();
}
