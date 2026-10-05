using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Uploads;

public partial class QueuedUploadList : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<RunningUploadReadModel> Uploads { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public EventCallback<RunningUploadReadModel> OnCancel { get; set; }
}
