using Bearcat.Abstractions.RemoteSource.Dto;

namespace Bearcat.Abstractions.RemoteSource;

public interface IRemoteSourceFactory
{
    IRemoteSource GetByClassName(string className);

    IReadOnlyList<RemoteSourceDto> GetRemoteSources();
}
