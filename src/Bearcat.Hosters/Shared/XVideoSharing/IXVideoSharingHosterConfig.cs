using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Shared.XVideoSharing;

public interface IXVideoSharingHosterConfig : IHosterConfig
{
    string ApiKey { get; }
}
