using Bearcat.Abstractions.LinkCrypter;

namespace Bearcat.LinkCrypters.Extensions;

public static class ConfigExtensions
{
    extension(ILinkCrypterConfig config)
    {
        public T As<T>()
        where T : ILinkCrypterConfig
        {
            return config is not T typedConfig
                ? throw new InvalidCastException($"Cannot cast config of type {config.GetType().FullName} to type {typeof(T).FullName}")
                : typedConfig;
        }
    }
}
