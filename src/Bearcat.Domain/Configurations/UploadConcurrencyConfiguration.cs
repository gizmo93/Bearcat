using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration("UploadConcurrency", "UploadConcurrency", "UploadConcurrencyDescription")]
public class UploadConcurrencyConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty("MaxParallelUploads", "MaxParallelUploadsDescription")]
    public int MaxParallelUploads { get; set; } = 10;

    [ApplicationConfigurationProperty(
        "UploadSpeedLimitMegabytesPerSecond",
        "UploadSpeedLimitMegabytesPerSecondDescription"
    )]
    [ApplicationConfigurationUnit(ApplicationConfigurationUnit.MegabytesPerSecond)]
    public decimal? UploadSpeedLimitMegabytesPerSecond { get; set; }

    [ApplicationConfigurationProperty("UploadStartOrder", "UploadStartOrderDescription")]
    public UploadStartOrder UploadStartOrder { get; set; } = UploadStartOrder.OldestFirst;
}
