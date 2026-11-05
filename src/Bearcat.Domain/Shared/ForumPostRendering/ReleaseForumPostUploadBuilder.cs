namespace Bearcat.Domain.Shared.ForumPostRendering;

/// <summary>
/// Builds the forum post upload models for a single release. Shared by the release and the
/// release collection render sources so both expose the same upload/link-crypter variables.
/// </summary>
public class ReleaseForumPostUploadBuilder(IReleaseForumPostUploadRepository repository)
{
    public async Task<IReadOnlyList<ForumPostTemplateUploadModel>> BuildAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var uploads = await repository.GetForumPostUploadsAsync(releaseId, cancellationToken);

        return uploads.Select(ToUploadModel).ToList();
    }

    public async Task<
        IReadOnlyList<ForumPostTemplateLinkCrypterModel>
    > BuildAllHostersLinkCryptersAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        var linkCrypters = await repository.GetForumPostAllHostersLinkCryptersAsync(
            releaseId,
            cancellationToken
        );

        return linkCrypters.Select(ToLinkCrypterModel).ToList();
    }

    private static ForumPostTemplateUploadModel ToUploadModel(
        ReleaseForumPostUploadReadModel upload
    )
    {
        return new ForumPostTemplateUploadModel
        {
            Name = upload.UploadConfigName,
            HosterName = upload.HosterName,
            HosterRegistrationName = upload.HosterRegistrationName,
            IsVideoUpload = upload.IsVideoUpload,
            UploadedAt = upload.UploadedAt,
            ArchiveFormat = upload.ArchiveFormat ?? string.Empty,
            ArchivePassword = upload.ArchivePassword ?? string.Empty,
            Links = upload.Links,
            EmbedLinks = upload.EmbedLinks,
            LinkCrypters = upload.LinkCrypters.Select(ToLinkCrypterModel).ToList(),
        };
    }

    private static ForumPostTemplateLinkCrypterModel ToLinkCrypterModel(
        ReleaseForumPostLinkCrypterReadModel linkCrypter
    )
    {
        return new ForumPostTemplateLinkCrypterModel
        {
            Name = linkCrypter.Name,
            Password = linkCrypter.Password ?? string.Empty,
            ContainerLink = linkCrypter.ContainerUrl,
            StatusImageId = linkCrypter.StatusImageId ?? string.Empty,
            CreatedAt = linkCrypter.CreatedAt,
        };
    }
}
