namespace Bearcat.Domain.Entities;

public class DistributionSiteRegistration
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string DistributionSiteClassName { get; set; }

    public required string SerializedConfig { get; set; }

    public bool IsActive { get; set; }

    public string? EncryptedSession { get; set; }
}
