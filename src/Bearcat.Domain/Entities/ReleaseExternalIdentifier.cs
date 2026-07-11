using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ReleaseExternalIdentifier
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }

    public Release Release { get; set; } = null!;

    public ExternalIdentifierType Type { get; set; }

    public string Value { get; set; } = null!;

    public ExternalIdentifierSource Source { get; set; }
}
