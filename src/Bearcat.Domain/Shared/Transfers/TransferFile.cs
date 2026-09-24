namespace Bearcat.Domain.Shared.Transfers;

public sealed record TransferFile(
    int FileId,
    string FileName,
    string SourceName,
    long? SizeBytes,
    bool IsAlreadyTransferred
);
