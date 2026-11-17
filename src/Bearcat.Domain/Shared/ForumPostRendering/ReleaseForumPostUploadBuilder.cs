namespace Bearcat.Domain.Shared.ForumPostRendering;

public static class ReleaseForumPostUploadBuilder
{
    public static ForumPostTemplateUploadModel ToUploadModel(ReleaseForumPostUploadReadModel upload)
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
