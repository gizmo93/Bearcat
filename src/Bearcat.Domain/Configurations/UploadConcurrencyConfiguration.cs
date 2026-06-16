using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration("UploadConcurrency", "UploadConcurrency", "UploadConcurrencyDescription")]
public class UploadConcurrencyConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty("MaxParallelUploads", "MaxParallelUploadsDescription")]
    public int MaxParallelUploads { get; set; } = 10;
}
