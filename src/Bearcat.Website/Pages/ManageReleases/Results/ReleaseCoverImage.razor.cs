using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseCoverImage : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ReleaseSearchResultReadModel Release { get; set; } = null!;

    [Parameter]
    public bool ShowMetadataTitle { get; set; }

    private string ContentTypeIconName =>
        ReleaseContentTypeIcons.GetIconName(Release.ReleaseContentType);
}
