using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageAdditionalArchiveContents.Assignment;

public partial class AssignedAdditionalArchiveContentBadges : ComponentBase
{
    [Parameter, EditorRequired]
    public IReadOnlyList<AssignedAdditionalArchiveContentReadModel> AdditionalArchiveContents { get; set; } =
    [];
}
