using Microsoft.Extensions.Configuration;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.Shared;

public sealed class ControllableTimeProvider(DateTime now)
    : TimeProvider(new ConfigurationBuilder().Build())
{
    public DateTime Now { get; set; } = now;

    public override DateTime GetLocalNow()
    {
        return Now;
    }
}
