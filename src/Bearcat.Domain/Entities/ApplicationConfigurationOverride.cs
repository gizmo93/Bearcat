namespace Bearcat.Domain.Entities;

public class ApplicationConfigurationOverride
{
    public int Id { get; set; }

    public string ConfigurationKey { get; set; } = null!;

    public string PropertyName { get; set; } = null!;

    public string SerializedValue { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }
}
