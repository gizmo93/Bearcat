namespace Bearcat.Domain.Shared.Transfers;

public sealed record PlannedTransferFile(
    int FileId,
    string FileName,
    string SourceName,
    long? SizeBytes,
    bool IsAlreadyTransferred
);
