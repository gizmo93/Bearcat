namespace Bearcat.Domain.Entities;

public class LinkCrypterContainer
{
    public int Id { get; set; }

    public int UploadConfigLinkCrypterId { get; set; }

    public UploadConfigLinkCrypter UploadConfigLinkCrypter { get; set; } = null!;

    public int UploadId { get; set; }

    public Upload Upload { get; set; } = null!;

    public string ExternalReference { get; set; } = null!;

    public string ContainerUrl { get; set; } = null!;
}
