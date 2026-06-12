using Bearcat.Abstractions.SeriesDatabase;

namespace Bearcat.SeriesDatabases.Extensions;

public static class ConfigExtensions
{
    extension(ISeriesDatabaseConfig config)
    {
        public T As<T>()
            where T : ISeriesDatabaseConfig
        {
            return config is not T typedConfig
                ? throw new InvalidCastException(
                    $"Cannot cast config of type {config.GetType().FullName} to type {typeof(T).FullName}"
                )
                : typedConfig;
        }
    }
}
