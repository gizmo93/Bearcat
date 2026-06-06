using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;

public interface IReleaseTemplateWriteRepository
{
    void Add(ReleaseTemplate releaseTemplate);

    void Remove(ReleaseTemplate releaseTemplate);

    void Remove(ArchiveConfigTemplate archiveConfigTemplate);

    void Remove(UploadConfigTemplate uploadConfigTemplate);

    void Remove(ImageUploadConfigTemplate imageUploadConfigTemplate);

    void Remove(UploadConfigLinkCrypterTemplate linkCrypterTemplate);

    Task<ReleaseTemplate> GetByIdAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseTemplate> GetByIdWithChildrenAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    );

    Task<Release> GetReleaseForTemplateCreationAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<ArchiveConfigTemplate> GetArchiveConfigTemplateAsync(
        int archiveConfigTemplateId,
        CancellationToken cancellationToken = default
    );

    Task<UploadConfigTemplate> GetUploadConfigTemplateAsync(
        int uploadConfigTemplateId,
        CancellationToken cancellationToken = default
    );

    Task<ImageUploadConfigTemplate> GetImageUploadConfigTemplateAsync(
        int imageUploadConfigTemplateId,
        CancellationToken cancellationToken = default
    );

    Task<UploadConfigLinkCrypterTemplate> GetUploadConfigLinkCrypterTemplateAsync(
        int uploadConfigLinkCrypterTemplateId,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
