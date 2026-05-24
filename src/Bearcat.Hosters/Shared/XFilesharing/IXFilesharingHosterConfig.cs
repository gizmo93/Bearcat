using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Shared.XFilesharing;

public interface IXFilesharingHosterConfig : IHosterConfig
{
    string ApiKey { get; }
}
