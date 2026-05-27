namespace Bearcat.Domain.Entities;

public class NfoDatabaseRegistration : IContainSerializedConfig
{
    public int Id { get; set; }

    public string NfoDatabaseClassName { get; set; } = null!;

    public string SerializedConfig { get; set; } = null!;

    public bool IsActive { get; set; }
}
