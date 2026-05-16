using Bearcat.Domain.Entities;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningUploads;

public partial class FileDetails : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public Upload Upload { get; set; } = null!;
}
