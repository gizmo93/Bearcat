namespace Bearcat.Domain.Entities;

public class LinkCrypterRegistration
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string LinkCrypterClassName { get; set; } = null!;

    public string SerializedConfig { get; set; } = null!;

    public bool IsActive { get; set; }
}
