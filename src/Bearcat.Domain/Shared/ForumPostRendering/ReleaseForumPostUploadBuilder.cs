namespace Bearcat.Domain.Shared.ForumPostRendering;

/// <summary>
/// Builds the forum post upload models of releases, keyed by release id. Shared by the release and
/// the release collection render sources so both expose the same upload/link-crypter variables.
/// </summary>
public class ReleaseForumPostUploadBuilder(IReleaseForumPostUploadRepository repository)
{
    public async Task<
        IReadOnlyDictionary<int, IReadOnlyList<ForumPostTemplateUploadModel>>
    > BuildByReleaseIdAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        var uploadsByReleaseId = await repository.GetForumPostUploadsByReleaseIdAsync(
            releaseIds,
            cancellationToken
        );

        return uploadsByReleaseId.ToDictionary(
            pair => pair.Key,
            pair =>
                (IReadOnlyList<ForumPostTemplateUploadModel>)
                    pair.Value.Select(ToUploadModel).ToList()
        );
    }

    public async Task<
        IReadOnlyDictionary<int, IReadOnlyList<ForumPostTemplateLinkCrypterModel>>
    > BuildAllHostersLinkCryptersByReleaseIdAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        var linkCryptersByReleaseId =
            await repository.GetForumPostAllHostersLinkCryptersByReleaseIdAsync(
                releaseIds,
                cancellationToken
            );

        return linkCryptersByReleaseId.ToDictionary(
            pair => pair.Key,
            pair =>
                (IReadOnlyList<ForumPostTemplateLinkCrypterModel>)
                    pair.Value.Select(ToLinkCrypterModel).ToList()
        );
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

    public static ForumPostTemplateLinkCrypterModel ToLinkCrypterModel(
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
