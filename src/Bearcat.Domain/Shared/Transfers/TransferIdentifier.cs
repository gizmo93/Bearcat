namespace Bearcat.Domain.Shared.Transfers;

public readonly record struct TransferIdentifier(TransferKind Kind, int Id);
