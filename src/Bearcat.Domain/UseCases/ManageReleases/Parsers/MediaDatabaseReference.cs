using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Parsers;

public record MediaDatabaseReference(ExternalIdentifierType Type, string Value)
{
    public string? GetValueWhenType(ExternalIdentifierType type)
    {
        return Type == type ? Value : null;
    }
}
