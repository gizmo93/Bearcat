using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class HosterAccountPoolUploadConfigTemplateLoadingTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private HosterAccountPool hosterAccountPool = null!;
    private UploadConfigTemplate uploadConfigTemplate = null!;

    [SetUp]
    public async Task AddHosterAccountPoolUploadConfigTemplateAsync()
    {
        var testData = new MaterializedValuesTestData(DbContext);
        hosterAccountPool = testData.AddHosterAccountPool(
            "Main",
            testData.AddHosterRegistration("Member")
        );
        uploadConfigTemplate = testData.AddHosterAccountPoolUploadConfigTemplate(hosterAccountPool);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    [Test]
    public async Task ReleaseWriteRepositoryGetTemplateForReleaseCreationOrDefaultAsync_HosterAccountPoolTemplate_LoadsHosterAccountPool()
    {
        // Arrange
        var repository = new ReleaseWriteRepository(CreateDbContext());

        // Act
        var result = await repository.GetTemplateForReleaseCreationOrDefaultAsync(
            uploadConfigTemplate.ReleaseTemplateId,
            CancellationToken.None
        );

        // Assert
        AssertLoadsHosterAccountPool(result!.UploadConfigTemplates.ShouldHaveSingleItem());
    }

    [Test]
    public async Task RemoteSourceDownloadRepositoryGetReleaseTemplateAsync_HosterAccountPoolTemplate_LoadsHosterAccountPool()
    {
        // Arrange
        var repository = new RemoteSourceDownloadRepository(CreateDbContext());

        // Act
        var result = await repository.GetReleaseTemplateAsync(
            uploadConfigTemplate.ReleaseTemplateId,
            CancellationToken.None
        );

        // Assert
        AssertLoadsHosterAccountPool(result!.UploadConfigTemplates.ShouldHaveSingleItem());
    }

    [Test]
    public async Task ReleaseFolderAutomationRepositoryGetEnabledWithTemplatesAsync_HosterAccountPoolTemplate_LoadsHosterAccountPool()
    {
        // Arrange
        DbContext.ReleaseFolderAutomations.Add(
            new ReleaseFolderAutomation
            {
                BasePath = "/incoming",
                ReleaseTemplateId = uploadConfigTemplate.ReleaseTemplateId,
                IsEnabled = true,
            }
        );
        await DbContext.SaveChangesAsync();
        var dbContext = CreateDbContext();
        var repository = new ReleaseFolderAutomationRepository(dbContext, dbContext);

        // Act
        var result = await repository.GetEnabledWithTemplatesAsync(CancellationToken.None);

        // Assert
        AssertLoadsHosterAccountPool(
            result
                .ShouldHaveSingleItem()
                .ReleaseTemplate.UploadConfigTemplates.ShouldHaveSingleItem()
        );
    }

    [Test]
    public async Task ReleaseTemplateRepositoryGetDetailAsync_HosterAccountPoolTemplate_ReturnsHosterAccountPoolAsUploadTarget()
    {
        // Arrange
        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(factory => factory.GetArchivers())
            .Returns([new ArchiverDto("RAR", "rar", ".rar")]);
        var linkCrypterFactory = new Mock<ILinkCrypterFactory>();
        linkCrypterFactory.Setup(factory => factory.GetLinkCrypters()).Returns([]);
        var dbContext = CreateDbContext();
        var repository = new ReleaseTemplateRepository(
            dbContext,
            dbContext,
            archiverFactory.Object,
            linkCrypterFactory.Object
        );

        // Act
        var result = await repository.GetDetailAsync(
            uploadConfigTemplate.ReleaseTemplateId,
            CancellationToken.None
        );

        // Assert
        var template = result!.UploadConfigTemplates.ShouldHaveSingleItem();
        template.DisplayName.ShouldBe("Main");
        template.HosterRegistrationId.ShouldBeNull();
        template.HosterRegistrationName.ShouldBeNull();
        template.HosterAccountPoolId.ShouldBe(hosterAccountPool.Id);
        template.HosterAccountPoolName.ShouldBe("Main");
    }

    [Test]
    public async Task ReleaseTemplateRepositoryGetReleaseForTemplateCreationAsync_HosterAccountPoolUploadConfig_LoadsHosterAccountPool()
    {
        // Arrange
        var testData = new MaterializedValuesTestData(DbContext);
        var release = testData.AddRelease("Release");
        testData.AddHosterAccountPoolUploadConfig(
            release,
            await DbContext.HosterAccountPools.SingleAsync(pool => pool.Id == hosterAccountPool.Id)
        );
        await DbContext.SaveChangesAsync();
        var dbContext = CreateDbContext();
        var repository = new ReleaseTemplateRepository(
            dbContext,
            dbContext,
            Mock.Of<IArchiverFactory>(),
            Mock.Of<ILinkCrypterFactory>()
        );

        // Act
        var result = await repository.GetReleaseForTemplateCreationAsync(
            release.Id,
            CancellationToken.None
        );

        // Assert
        var uploadConfig = result.UploadConfigs.ShouldHaveSingleItem();
        uploadConfig.HosterAccountPool.ShouldNotBeNull();
        uploadConfig.GetUploadTargetName().ShouldBe("Main");
    }

    private void AssertLoadsHosterAccountPool(UploadConfigTemplate loadedUploadConfigTemplate)
    {
        loadedUploadConfigTemplate.HosterRegistration.ShouldBeNull();
        loadedUploadConfigTemplate.HosterAccountPool.ShouldNotBeNull();
        loadedUploadConfigTemplate.HosterAccountPool.Id.ShouldBe(hosterAccountPool.Id);
        loadedUploadConfigTemplate.GetUploadTargetName().ShouldBe("Main");
    }
}
