using Bearcat.Abstractions.MediaMetadataDatabase;

namespace Bearcat.SeriesDatabases.Extensions;

public static class ConfigExtensions
{
    extension(IMediaMetadataDatabaseConfig config)
    {
        public T As<T>()
            where T : IMediaMetadataDatabaseConfig
        {
            return config is not T typedConfig
                ? throw new InvalidCastException(
                    $"Cannot cast config of type {config.GetType().FullName} to type {typeof(T).FullName}"
                )
                : typedConfig;
        }
    }
}
