namespace Bearcat.Domain.Entities;

public class MediaDatabaseRegistration
{
    public int Id { get; set; }

    public string MediaDatabaseClassName { get; set; } = null!;

    public string SerializedConfig { get; set; } = null!;

    public bool IsActive { get; set; }
}
