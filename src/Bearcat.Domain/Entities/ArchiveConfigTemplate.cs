namespace Bearcat.Domain.Entities;

public class ArchiveConfigTemplate
{
    public int Id { get; set; }

    public int ReleaseTemplateId { get; set; }

    public ReleaseTemplate ReleaseTemplate { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string ArchiveFilesBasePath { get; set; } = null!;

    public string ArchiverName { get; set; } = null!;

    public string? ArchivePassword { get; set; }

    public int ArchiveFileSizeMb { get; set; }

    public bool UseReleaseNameAsArchiveName { get; set; }

    public List<UploadConfigTemplate> UploadConfigTemplates { get; set; } = [];
}
