using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Shared.CostAction;

public interface ICostActionHosterConfig : IHosterConfig
{
    string ApiKey { get; }
}
