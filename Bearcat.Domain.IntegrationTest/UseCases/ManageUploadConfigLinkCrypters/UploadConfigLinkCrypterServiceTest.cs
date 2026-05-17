using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploadConfigLinkCrypters;

public class UploadConfigLinkCrypterServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private UploadConfigLinkCrypterService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new UploadConfigLinkCrypterService(
            new UploadConfigLinkCrypterWriteRepository(dbContext)
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidLinkCrypter_PersistsUploadConfigLinkCrypter()
    {
        // Arrange
        var seed = await AddDependenciesAsync();

        // Act
        await service.CreateAsync(
            seed.UploadConfigId,
            seed.LinkCrypterRegistrationId,
            "secret",
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.UploadConfigLinkCrypters.SingleAsync();

        result.ShouldNotBeNull();
        result.UploadConfigId.ShouldBe(seed.UploadConfigId);
        result.LinkCrypterRegistrationId.ShouldBe(seed.LinkCrypterRegistrationId);
        result.Password.ShouldBe("secret");
    }

    [Test]
    public async Task CreateAsync_PasswordIsBlank_PersistsNullPassword()
    {
        // Arrange
        var seed = await AddDependenciesAsync();

        // Act
        await service.CreateAsync(
            seed.UploadConfigId,
            seed.LinkCrypterRegistrationId,
            " ",
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.UploadConfigLinkCrypters.SingleAsync();

        result.ShouldNotBeNull();
        result.Password.ShouldBeNull();
    }

    [Test]
    public async Task UpdateAsync_LinkCrypterExists_UpdatesPassword()
    {
        // Arrange
        var linkCrypter = await AddUploadConfigLinkCrypterAsync("old-secret");

        // Act
        await service.UpdateAsync(linkCrypter.Id, "new-secret", CancellationToken.None);

        // Assert
        var result = await dbContext.UploadConfigLinkCrypters.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(linkCrypter.Id);
        result.Password.ShouldBe("new-secret");
    }

    [Test]
    public async Task UpdateAsync_PasswordIsBlank_UpdatesPasswordToNull()
    {
        // Arrange
        var linkCrypter = await AddUploadConfigLinkCrypterAsync("old-secret");

        // Act
        await service.UpdateAsync(linkCrypter.Id, "", CancellationToken.None);

        // Assert
        var result = await dbContext.UploadConfigLinkCrypters.SingleAsync();

        result.ShouldNotBeNull();
        result.Password.ShouldBeNull();
    }

    [Test]
    public async Task DeleteAsync_LinkCrypterExists_RemovesUploadConfigLinkCrypter()
    {
        // Arrange
        var linkCrypter = await AddUploadConfigLinkCrypterAsync("secret");

        // Act
        await service.DeleteAsync(linkCrypter.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.UploadConfigLinkCrypters.AnyAsync();

        result.ShouldBeFalse();
    }

    private async Task<UploadConfigLinkCrypter> AddUploadConfigLinkCrypterAsync(string? password)
    {
        var seed = await AddDependenciesAsync();
        var linkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfigId = seed.UploadConfigId,
            LinkCrypterRegistrationId = seed.LinkCrypterRegistrationId,
            Password = password,
        };

        dbContext.UploadConfigLinkCrypters.Add(linkCrypter);
        await dbContext.SaveChangesAsync();

        return linkCrypter;
    }

    private async Task<UploadConfigLinkCrypterSeed> AddDependenciesAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroup = releaseGroup,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/tmp/archive",
            ArchiverName = "zip",
            ArchiveNamePrefix = "bearcat-release",
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = "Default upload",
            LinksDistributedTo = [],
        };
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Crypter",
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };

        dbContext.UploadConfigs.Add(uploadConfig);
        dbContext.LinkCrypterRegistrations.Add(linkCrypterRegistration);
        await dbContext.SaveChangesAsync();

        return new UploadConfigLinkCrypterSeed(uploadConfig.Id, linkCrypterRegistration.Id);
    }

    private sealed record UploadConfigLinkCrypterSeed(
        int UploadConfigId,
        int LinkCrypterRegistrationId
    );
}
