namespace Bearcat.Domain.Entities;

public class LinkCrypterImportHosterRegistration
{
    public int Id { get; set; }

    public int LinkCrypterImportId { get; set; }

    public LinkCrypterImport LinkCrypterImport { get; set; } = null!;

    public string HosterClassName { get; set; } = null!;

    public int? HosterRegistrationId { get; set; }

    public HosterRegistration? HosterRegistration { get; set; }
}
