using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Extensions;

public static class ConfigExtensions
{
    extension(IHosterConfig config)
    {
        public T As<T>()
            where T : IHosterConfig
        {
            return config is not T typedConfig
                ? throw new InvalidCastException(
                    $"Cannot cast config of type {config.GetType().FullName} to type {typeof(T).FullName}"
                )
                : typedConfig;
        }
    }
}
