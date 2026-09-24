using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.RemoteSource.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.RemoteSources;

public class RemoteSourceFactory(IServiceProvider serviceProvider) : IRemoteSourceFactory
{
    public IRemoteSource GetByClassName(string className)
    {
        return serviceProvider.GetRequiredKeyedService<IRemoteSource>(className);
    }

    public IReadOnlyList<RemoteSourceDto> GetRemoteSources()
    {
        var remoteSources = serviceProvider.GetKeyedServices<IRemoteSource>(KeyedService.AnyKey);

        return remoteSources
            .Select(remoteSource => new RemoteSourceDto(
                Name: remoteSource.Name,
                ClassName: remoteSource.GetType().Name,
                ConfigurationFields: remoteSource.ConfigurationFields
            ))
            .ToList();
    }
}
