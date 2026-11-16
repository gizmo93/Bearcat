using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ChangeArchivePartSize;

public partial class SkippedArchiveConfigTemplateList : ComponentBase
{
    private const string RowGridClass =
        "lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1.25fr)]";

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<SkippedArchiveConfigTemplateReadModel> ArchiveConfigTemplates { get; set; } =
        null!;
}
