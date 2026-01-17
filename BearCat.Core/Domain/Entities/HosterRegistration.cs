namespace BearCat.Core.Domain.Entities;

public class HosterRegistration
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string SerializedConfig { get; set; }

    public bool IsActive { get; set; }

    public required string HosterClassName { get; set; }

    public List<UploadConfig> UploadConfigs { get; set; } = null!;
}
