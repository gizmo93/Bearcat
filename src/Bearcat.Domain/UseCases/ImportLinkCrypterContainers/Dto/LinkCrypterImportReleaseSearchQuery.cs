namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;

public sealed record LinkCrypterImportReleaseSearchQuery(
    LinkCrypterImportReleaseFilter Filter,
    int PageIndex,
    int PageSize
);
