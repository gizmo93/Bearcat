using Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ReplaceHosters;

public partial class SkippedUploadConfigList : ComponentBase
{
    private const int MaximumRenderedRows = 100;
    private const string RowGridClass =
        "lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1.25fr)]";

    [Parameter]
    [EditorRequired]
    public string Heading { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<SkippedUploadConfigReadModel> UploadConfigs { get; set; } = null!;

    [Parameter]
    public bool IsDeletion { get; set; }

    private string HeadingClass =>
        IsDeletion
            ? "flex items-center gap-2 text-sm font-semibold text-destructive"
            : "flex items-center gap-2 text-sm font-semibold";
}
