namespace Bearcat.Domain.Shared.Transfers;

public readonly record struct TransferKey(TransferKind Kind, int Id);
