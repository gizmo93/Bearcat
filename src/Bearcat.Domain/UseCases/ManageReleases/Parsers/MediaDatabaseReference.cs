using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Parsers;

public record MediaDatabaseReference(ExternalIdentifierType Type, string Value);
