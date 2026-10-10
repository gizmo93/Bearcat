using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class ArchiveStorageFolderBadge : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string Name { get; set; } = null!;

    [Parameter]
    public bool HasLocalWorkingCopyFiles { get; set; }

    private string Title =>
        HasLocalWorkingCopyFiles
            ? L["ArchiveStorageFolderWithLocalWorkingCopy"]
            : L["ArchiveStorageFolder"];
}
