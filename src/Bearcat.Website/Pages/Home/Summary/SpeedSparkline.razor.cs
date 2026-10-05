using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Summary;

public partial class SpeedSparkline : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public TransferSpeedHistory History { get; set; } = null!;

    private SparklinePoints? Points =>
        SparklinePointsBuilder.Build(History.Samples, History.Capacity);
}
