namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public class ArchiveConfigFormModel
{
    public string? ArchiveFilesBasePath { get; set; }
    
    public string? ArchiverName { get; set; }
    
    public string? ArchiveNamePrefix { get; set; }
    
    public string? ArchivePassword { get; set; }
    
    public int? ArchiveFileSizeMb { get; set; }
    
    public bool IsEdit { get; set; }
}
