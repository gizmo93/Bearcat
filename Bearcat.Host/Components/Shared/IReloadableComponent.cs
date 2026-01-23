namespace Bearcat.Host.Components.Shared;

public interface IReloadableComponent
{
    Task ReloadAsync();
}
