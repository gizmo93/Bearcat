namespace Bearcat.Domain.Entities;

public class SeriesDatabaseRegistration
{
    public int Id { get; set; }

    public string SeriesDatabaseClassName { get; set; } = null!;

    public string SerializedConfig { get; set; } = null!;

    public bool IsActive { get; set; }
}
