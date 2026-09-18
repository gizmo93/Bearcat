using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Uploads;

public record LinkCrypterLinkResponse(
    int Id,
    string LinkCrypterRegistrationName,
    string LinkCrypterClassName,
    string ContainerUrl,
    string? StatusImageId,
    LinkCrypterContainerScope Scope,
    LinkCrypterContainerState State,
    DateTime CreatedAt,
    bool EnableCaptcha,
    bool EnableContainerDownload,
    bool EnableClickAndLoad,
    bool SupportsCaptcha,
    bool SupportsContainerDownload,
    bool SupportsClickAndLoad,
    IReadOnlyList<string> Errors
)
{
    public static LinkCrypterLinkResponse FromReadModel(
        ReleaseUploadContainerLinkReadModel readModel
    )
    {
        return new LinkCrypterLinkResponse(
            Id: readModel.Id,
            LinkCrypterRegistrationName: readModel.LinkCrypterRegistrationName,
            LinkCrypterClassName: readModel.LinkCrypterClassName,
            ContainerUrl: readModel.ContainerUrl,
            StatusImageId: readModel.StatusImageId,
            Scope: readModel.Scope,
            State: readModel.State,
            CreatedAt: readModel.CreatedAt,
            EnableCaptcha: readModel.EnableCaptcha,
            EnableContainerDownload: readModel.EnableContainerDownload,
            EnableClickAndLoad: readModel.EnableClickAndLoad,
            SupportsCaptcha: readModel.SupportsCaptcha,
            SupportsContainerDownload: readModel.SupportsContainerDownload,
            SupportsClickAndLoad: readModel.SupportsClickAndLoad,
            Errors: readModel.Errors
        );
    }
}
