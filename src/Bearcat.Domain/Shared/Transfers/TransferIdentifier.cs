namespace Bearcat.Domain.Shared.Transfers;

public readonly record struct TransferIdentifier(TransferType Type, int Id);
