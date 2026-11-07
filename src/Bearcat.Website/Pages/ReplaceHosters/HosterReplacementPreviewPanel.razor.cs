using Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ReplaceHosters;

public partial class HosterReplacementPreviewPanel : ComponentBase
{
    private const string DeletedCollectionUploadSlotGridClass =
        "lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1.25fr)]";

    [Parameter]
    [EditorRequired]
    public HosterReplacementPreview Preview { get; set; } = null!;
}
