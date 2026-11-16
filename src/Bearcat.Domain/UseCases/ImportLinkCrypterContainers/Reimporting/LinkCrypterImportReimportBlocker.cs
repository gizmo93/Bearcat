using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public sealed record LinkCrypterImportReimportBlocker(
    LinkCrypterImportReimportBlockerReason Reason,
    string? Detail,
    LinkCrypterImportNotImportableReason? NotImportableReason
);
