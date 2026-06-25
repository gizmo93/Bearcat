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

    private static ForumPostTemplateUploadModel ToUploadModel(
        ReleaseForumPostUploadReadModel upload
    )
    {
        return new ForumPostTemplateUploadModel
        {
            Name = upload.UploadConfigName,
            HosterName = upload.HosterName,
            UploadedAt = upload.UploadedAt,
            ArchiveFormat = upload.ArchiveFormat,
            ArchivePassword = upload.ArchivePassword ?? string.Empty,
            Links = upload.Links,
            LinkCrypters = upload
                .LinkCrypters.Select(linkCrypter => new ForumPostTemplateLinkCrypterModel
                {
                    Name = linkCrypter.Name,
                    Password = linkCrypter.Password ?? string.Empty,
                    ContainerLink = linkCrypter.ContainerUrl,
                    StatusImageId = linkCrypter.StatusImageId ?? string.Empty,
                    CreatedAt = linkCrypter.CreatedAt,
                })
                .ToList(),
        };
    }
}
