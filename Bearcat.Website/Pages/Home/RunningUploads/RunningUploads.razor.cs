using Bearcat.Domain.Entities;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningUploads;

public partial class RunningUploads : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<Upload> Uploads { get; set; } = null!;

    private readonly HashSet<int> showDetailIds = [];


    private void ToggleShowUploadDetails(int uploadId)
    {
        if (!showDetailIds.Remove(uploadId))
        {
            showDetailIds.Add(uploadId);
        }

        StateHasChanged();
    }
}

