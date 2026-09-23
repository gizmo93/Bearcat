using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageRemoteSources.Sessions;

public class RemoteSourceSessionOpener(
    IRemoteSourceFactory remoteSourceFactory,
    ISecretProtector secretProtector
)
{
    public async Task<IRemoteSourceSession> OpenAsync(
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
