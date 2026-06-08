namespace Bearcat.Domain.Entities;

public class LinkCrypterContainerSourceUpload
{
    public int LinkCrypterContainerId { get; set; }

    public LinkCrypterContainer LinkCrypterContainer { get; set; } = null!;

    public int UploadId { get; set; }

    public Upload Upload { get; set; } = null!;
}
