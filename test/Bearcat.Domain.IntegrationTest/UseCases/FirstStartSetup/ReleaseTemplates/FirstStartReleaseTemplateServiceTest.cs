using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.FirstStartSetup.ReleaseTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.FileSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.FirstStartSetup.ReleaseTemplates;

public class FirstStartReleaseTemplateServiceTest(DatabaseProvider databaseProvider)
    : ReleaseTemplateIntegrationTest(databaseProvider)
{
    [Test]
    public async Task CreateReleaseGroupAndTemplateAsync_ValidInput_CreatesGroupTemplateArchiveAndUploadConfigTemplates()
    {
        // Arrange
        var primaryHosterRegistration = await AddHosterRegistrationAsync("Primary hoster");
        var secondaryHosterRegistration = await AddHosterRegistrationAsync("Secondary hoster");
        var archiveOutputFolderPath = Path.GetTempPath();
        var archiverFactoryMock = new Mock<IArchiverFactory>();
        archiverFactoryMock
            .Setup(factory => factory.GetArchivers())
            .Returns([new ArchiverDto("RAR", "RarArchiver", ".rar")]);
        var service = new FirstStartReleaseTemplateService(
            CreateRepository(),
            archiverFactoryMock.Object,
            new FileSystemService(),
            Options.Create(new WorkingDirectoriesConfig())
        );

        // Act
        var releaseTemplateId = await service.CreateReleaseGroupAndTemplateAsync(
            new FirstStartReleaseTemplateInput(
                ReleaseGroupName: " My uploads ",
                EnableAutomaticReuploads: true,
                NumberOfHoursUntilReupload: 24,
                ReleaseTemplateName: "Default template",
                ArchiverName: "RarArchiver",
                ArchiveFileSizeMb: 100,
                ArchivePassword: " secret ",
                ArchiveOutputFolderPath: archiveOutputFolderPath,
                HosterRegistrationIds:
                [
                    primaryHosterRegistration.Id,
                    secondaryHosterRegistration.Id,
                ]
            ),
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var releaseTemplate = await DbContext
            .ReleaseTemplates.Include(template => template.ReleaseGroup)
            .Include(template => template.ArchiveConfigTemplates)
            .Include(template => template.UploadConfigTemplates)
            .SingleAsync();
        var archiveConfigTemplate = releaseTemplate.ArchiveConfigTemplates.ShouldHaveSingleItem();

        releaseTemplate.Id.ShouldBe(releaseTemplateId);
        releaseTemplate.Name.ShouldBe("Default template");
        releaseTemplate.ReleaseType.ShouldBe(ReleaseType.Managed);
        releaseTemplate.ReleaseContentType.ShouldBe(ReleaseContentType.Movie);
        releaseTemplate.ReleaseGroup.Name.ShouldBe("My uploads");
        releaseTemplate.ReleaseGroup.EnableAutomaticReuploads.ShouldBeTrue();
        releaseTemplate.ReleaseGroup.NumberOfHoursUntilReupload.ShouldBe(24);
        archiveConfigTemplate.Name.ShouldBe("RAR");
        archiveConfigTemplate.ArchiverName.ShouldBe("RarArchiver");
        archiveConfigTemplate.ArchiveFilesBasePath.ShouldBe(archiveOutputFolderPath);
        archiveConfigTemplate.ArchiveFileSizeMb.ShouldBe(100);
        archiveConfigTemplate.ArchivePassword.ShouldBe("secret");
        archiveConfigTemplate.UseReleaseNameAsArchiveName.ShouldBeTrue();
        archiveConfigTemplate.PackReleaseFolderAsRootFolder.ShouldBeTrue();
        archiveConfigTemplate.CreateNonceFile.ShouldBeTrue();
        releaseTemplate
            .UploadConfigTemplates.Select(uploadConfigTemplate =>
                (
                    uploadConfigTemplate.HosterRegistrationId,
                    uploadConfigTemplate.ArchiveConfigTemplateId,
                    uploadConfigTemplate.Name
                )
            )
            .ShouldBe(
                [
                    (primaryHosterRegistration.Id, archiveConfigTemplate.Id, null),
                    (secondaryHosterRegistration.Id, archiveConfigTemplate.Id, null),
                ],
                ignoreOrder: true
            );
    }
}
