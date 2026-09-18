using Bearcat.Domain.UseCases.ManageReleases.ReadModels;

namespace Bearcat.Api.Contracts.Releases;

public record ReleaseMetadataResponse(
    string Source,
    string Title,
    string? Genre,
    string? Description,
    string? CoverUrl,
    string? MetadataDatabaseUrl
)
{
    public static ReleaseMetadataResponse FromReadModel(ReleaseMetadataReadModel readModel)
    {
        return new ReleaseMetadataResponse(
            readModel.MetadataDatabaseClassName,
            readModel.Title,
            readModel.Genre,
            readModel.Description,
            readModel.CoverUrl,
            readModel.MetadataDatabaseUrl
        );
    }
}
