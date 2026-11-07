using Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ReplaceHosters;

public partial class HosterReplacementPreviewPanel : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public HosterReplacementPreview Preview { get; set; } = null!;
}
