using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupWorkingDirectoriesStep : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public List<WorkingDirectoryStatus> WorkingDirectoryStatuses { get; set; } = null!;

    [Parameter]
    public EventCallback OnCheckAgain { get; set; }

    [Parameter]
    public EventCallback OnBack { get; set; }

    [Parameter]
    public EventCallback OnSkip { get; set; }

    [Parameter]
    public EventCallback OnNext { get; set; }

    private readonly bool isRunningInContainer = string.Equals(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        "true",
        StringComparison.OrdinalIgnoreCase
    );
}
