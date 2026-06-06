namespace Bearcat.Domain.Entities;

public class ImageHosterRegistration
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string ImageHosterClassName { get; set; } = null!;

    public string SerializedConfig { get; set; } = null!;

    public bool IsActive { get; set; }
}
