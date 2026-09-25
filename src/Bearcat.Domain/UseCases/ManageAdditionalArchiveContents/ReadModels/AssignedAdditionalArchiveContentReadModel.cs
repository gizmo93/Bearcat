using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;

public record AssignedAdditionalArchiveContentReadModel(
    int AdditionalArchiveContentId,
    string Name,
    AdditionalArchiveContentType Type
);
