using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageRemoteSources.Sessions;

public class RemoteSourceSessionProvider(
    RemoteSourceSessionPool sessionPool,
    IRemoteSourceFactory remoteSourceFactory,
    ISecretProtector secretProtector
)
{
    public Task<T> UseSessionAsync<T>(
        RemoteSourceRegistration registration,
        Func<IRemoteSourceSession, Task<T>> action,
        CancellationToken cancellationToken
    )
    {
        return sessionPool.UseSessionAsync(
            registration.Id,
            registration.MaxConnections,
            token => OpenSessionAsync(registration, token),
            action,
            cancellationToken
        );
    }

    public Task CloseSessionsAsync(int registrationId)
    {
        return sessionPool.CloseSessionsAsync(registrationId);
    }

    private async Task<IRemoteSourceSession> OpenSessionAsync(
        RemoteSourceRegistration registration,
        CancellationToken cancellationToken
    )
    {
        var remoteSource = remoteSourceFactory.GetByClassName(registration.SourceClassName);
        var config = remoteSource.DeserializeConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        return await remoteSource.OpenSessionAsync(config, cancellationToken);
    }
}
