using BearCat.Core.Domain.Entities;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningArchives;

public partial class RunningArchives : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<Archive> Archives { get; set; } = null!;
}

