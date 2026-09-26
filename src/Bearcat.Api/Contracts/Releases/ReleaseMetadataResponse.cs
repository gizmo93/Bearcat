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
            Source: readModel.MetadataDatabaseClassName,
            Title: readModel.Title,
            Genre: readModel.Genre,
            Description: readModel.Description,
            CoverUrl: readModel.CoverUrl,
            MetadataDatabaseUrl: readModel.MetadataDatabaseUrl
        );
    }
}
