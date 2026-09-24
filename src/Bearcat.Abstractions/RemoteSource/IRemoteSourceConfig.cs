namespace Bearcat.Abstractions.RemoteSource;

public interface IRemoteSourceConfig
{
    IReadOnlyDictionary<string, object?> ToDictionary();
}
