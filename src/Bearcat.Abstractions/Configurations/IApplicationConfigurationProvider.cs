using System.Linq.Expressions;

namespace Bearcat.Abstractions.Configurations;

public interface IApplicationConfigurationProvider
{
    TConfiguration GetConfiguration<TConfiguration>()
        where TConfiguration : IApplicationConfiguration, new();

    bool GetValue<TConfiguration>(Expression<Func<TConfiguration, bool>> propertySelector)
        where TConfiguration : IApplicationConfiguration, new();

    int GetValue<TConfiguration>(Expression<Func<TConfiguration, int>> propertySelector)
        where TConfiguration : IApplicationConfiguration, new();

    int? GetValue<TConfiguration>(Expression<Func<TConfiguration, int?>> propertySelector)
        where TConfiguration : IApplicationConfiguration, new();

    string? GetValue<TConfiguration>(Expression<Func<TConfiguration, string?>> propertySelector)
        where TConfiguration : IApplicationConfiguration, new();

    TValue GetValue<TConfiguration, TValue>(
        Expression<Func<TConfiguration, TValue>> propertySelector
    )
        where TConfiguration : IApplicationConfiguration, new();
}
