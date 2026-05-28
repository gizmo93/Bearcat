namespace Bearcat.Domain.Entities;

public class HosterRegistration : IContainSerializedConfig
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string SerializedConfig { get; set; }

    public bool IsActive { get; set; }

    public bool RequiresCaptchaVerification { get; set; }

    public required string HosterClassName { get; set; }

    public List<UploadConfig> UploadConfigs { get; set; } = null!;
}
