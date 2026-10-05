using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseTags : ComponentBase
{
    private const string TagBaseClass =
        "inline-flex items-center rounded px-1.5 text-[0.6875rem] font-medium leading-[1.125rem]";

    private const string MutedTagClass = $"{TagBaseClass} bg-muted text-muted-foreground";
    private const string OutlineTagClass =
        $"{TagBaseClass} border border-border text-muted-foreground";
    private const string PrimaryTagClass = $"{TagBaseClass} bg-primary/12 text-primary";

    [Parameter]
    [EditorRequired]
    public ReleaseSearchResultReadModel Release { get; set; } = null!;

    [Parameter]
    public bool ShowReleaseGroup { get; set; }
}
