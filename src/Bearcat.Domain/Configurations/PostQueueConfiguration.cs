using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration("PostQueue", "PostQueue", "PostQueueConfigurationDescription")]
public class PostQueueConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty("PostQueueEnabled", "PostQueueEnabledDescription")]
    public bool Enabled { get; set; } = true;
}
