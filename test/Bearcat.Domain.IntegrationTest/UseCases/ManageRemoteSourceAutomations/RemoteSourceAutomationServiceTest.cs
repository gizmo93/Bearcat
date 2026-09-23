using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageRemoteSourceAutomations;

public class RemoteSourceAutomationServiceTest : BearcatIntegrationTest
{
    private const string TargetPath = "/data/downloads";

    private BearcatDbContext dbContext = null!;
    private RemoteSourceAutomationRepository repository = null!;
    private RemoteSourceAutomationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        repository = new RemoteSourceAutomationRepository(dbContext, dbContext);
        service = new RemoteSourceAutomationService(repository);
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidInput_PersistsNormalizedAutomation()
    {
        // Arrange
        var registration = await AddRegistrationAsync("Main FTP");
        var template = await AddReleaseTemplateAsync("Managed", ReleaseType.Managed);

        // Act
        var id = await service.CreateAsync(
            CreateInput(registration, template) with
            {
                Name = "  Scene TV  ",
                RemotePath = " incoming/tv/ ",
                TargetPath = $" {TargetPath} ",
                FolderNamePattern = " *.German.* ",
                PrimaryLanguageCode = " DE ",
                KeepRawFiles = false,
                Priority = 50,
                IgnoreExistingOnFirstScan = true,
            }
        );

        // Assert
        var automation = await ReloadAsync(id);
        automation.Name.ShouldBe("Scene TV");
        automation.RemoteSourceRegistrationId.ShouldBe(registration.Id);
        automation.RemotePath.ShouldBe("/incoming/tv");
        automation.TargetPath.ShouldBe(TargetPath);
        automation.FolderNamePattern.ShouldBe("*.German.*");
        automation.ReleaseTemplateId.ShouldBe(template.Id);
        automation.PrimaryLanguageCode.ShouldBe("de");
        automation.KeepRawFiles.ShouldBeFalse();
        automation.Priority.ShouldBe(50);
        automation.IsEnabled.ShouldBeTrue();
        automation.IgnoreExistingOnFirstScan.ShouldBeTrue();
        automation.HasCompletedInitialScan.ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_EmptyPatternAndLanguage_StoresNullsAndDefaultPriority()
    {
        // Arrange
        var registration = await AddRegistrationAsync("Main FTP");
        var template = await AddReleaseTemplateAsync("Managed", ReleaseType.Managed);

        // Act
        var id = await service.CreateAsync(
            CreateInput(registration, template) with
            {
                FolderNamePattern = " ",
                PrimaryLanguageCode = "",
            }
        );

        // Assert
        var automation = await ReloadAsync(id);
        automation.FolderNamePattern.ShouldBeNull();
        automation.PrimaryLanguageCode.ShouldBeNull();
        automation.KeepRawFiles.ShouldBeTrue();
        automation.Priority.ShouldBe(RemoteSourceAutomation.DefaultPriority);
    }

    [Test]
    public async Task UpdateAsync_ChangedValues_UpdatesAutomation()
    {
        // Arrange
        var registration = await AddRegistrationAsync("Main FTP");
        var firstTemplate = await AddReleaseTemplateAsync("First", ReleaseType.Managed);
        var secondTemplate = await AddReleaseTemplateAsync("Second", ReleaseType.Unmanaged);
        var id = await service.CreateAsync(CreateInput(registration, firstTemplate));

        // Act
        await service.UpdateAsync(
            id,
            CreateInput(registration, secondTemplate) with
            {
                Name = "Scene Movies",
                TargetPath = "/data/movies",
                FolderNamePattern = "*2160p*",
                PrimaryLanguageCode = "en",
                Priority = 10,
                IgnoreExistingOnFirstScan = true,
            }
        );

        // Assert
        var automation = await ReloadAsync(id);
        automation.Name.ShouldBe("Scene Movies");
        automation.TargetPath.ShouldBe("/data/movies");
        automation.FolderNamePattern.ShouldBe("*2160p*");
        automation.ReleaseTemplateId.ShouldBe(secondTemplate.Id);
        automation.PrimaryLanguageCode.ShouldBe("en");
        automation.Priority.ShouldBe(10);
        automation.IgnoreExistingOnFirstScan.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_RemotePathChanged_ResetsInitialScan()
    {
        // Arrange
        var registration = await AddRegistrationAsync("Main FTP");
        var template = await AddReleaseTemplateAsync("Template", ReleaseType.Managed);
        var id = await service.CreateAsync(CreateInput(registration, template));
        await MarkInitialScanCompletedAsync(id);

        // Act
        await service.UpdateAsync(
            id,
            CreateInput(registration, template) with
            {
                RemotePath = "/archive",
            }
        );

        // Assert
        (await ReloadAsync(id)).HasCompletedInitialScan.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_OnlyPatternChanged_KeepsInitialScanState()
    {
        // Arrange
        var registration = await AddRegistrationAsync("Main FTP");
        var template = await AddReleaseTemplateAsync("Template", ReleaseType.Managed);
        var id = await service.CreateAsync(CreateInput(registration, template));
        await MarkInitialScanCompletedAsync(id);

        // Act
        await service.UpdateAsync(
            id,
            CreateInput(registration, template) with
            {
                RemotePath = "incoming/",
                FolderNamePattern = "*1080p*",
            }
        );

        // Assert
        (await ReloadAsync(id)).HasCompletedInitialScan.ShouldBeTrue();
    }

    [TestCase(" ", "/incoming", TargetPath)]
    [TestCase("Scene TV", " ", TargetPath)]
    [TestCase("Scene TV", "/incoming", " ")]
    [TestCase("Scene TV", "/incoming", "relative/downloads")]
    public async Task CreateAsync_MissingOrInvalidInput_ThrowsAndStoresNothing(
        string name,
        string remotePath,
        string targetPath
    )
    {
        // Arrange
        var registration = await AddRegistrationAsync("Main FTP");
        var template = await AddReleaseTemplateAsync("Template", ReleaseType.Managed);

        // Act
        var action = () =>
            service.CreateAsync(
                CreateInput(registration, template) with
                {
                    Name = name,
                    RemotePath = remotePath,
                    TargetPath = targetPath,
                }
            );

        // Assert
        await Should.ThrowAsync<ArgumentException>(action);
        (await CreateDbContext().RemoteSourceAutomations.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task CreateAsync_NegativePriority_Throws()
    {
        // Arrange
        var registration = await AddRegistrationAsync("Main FTP");
        var template = await AddReleaseTemplateAsync("Template", ReleaseType.Managed);

        // Act
        var action = () =>
            service.CreateAsync(CreateInput(registration, template) with { Priority = -1 });

        // Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(action);
    }

    [Test]
    public async Task CreateAsync_UnknownReleaseTemplate_Throws()
    {
        // Arrange
        var registration = await AddRegistrationAsync("Main FTP");
        var template = await AddReleaseTemplateAsync("Template", ReleaseType.Managed);

        // Act
        var action = () =>
            service.CreateAsync(
                CreateInput(registration, template) with
                {
                    ReleaseTemplateId = template.Id + 1000,
                }
            );

        // Assert
        var exception = await Should.ThrowAsync<ArgumentException>(action);
        exception.Message.ShouldStartWith("The selected release template does not exist.");
    }

    [Test]
    public async Task CreateAsync_UnknownRegistration_Throws()
    {
        // Arrange
        var registration = await AddRegistrationAsync("Main FTP");
        var template = await AddReleaseTemplateAsync("Template", ReleaseType.Managed);

        // Act
        var action = () =>
            service.CreateAsync(
                CreateInput(registration, template) with
                {
                    RemoteSourceRegistrationId = registration.Id + 1000,
                }
            );

        // Assert
        var exception = await Should.ThrowAsync<ArgumentException>(action);
        exception.Message.ShouldStartWith("The selected remote source does not exist.");
    }

    [Test]
    public async Task SetEnabledAsync_EnabledAutomation_Disables()
    {
        // Arrange
        var id = await CreateAutomationAsync();

        // Act
        await service.SetEnabledAsync(id, false);

        // Assert
        (await ReloadAsync(id)).IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task DeleteAsync_Automation_RemovesObservingDownloadsButKeepsOthers()
    {
        // Arrange
        var id = await CreateAutomationAsync();
        var automation = await ReloadAsync(id);
        await AddDownloadAsync(
            automation,
            "Observing.Release",
            RemoteSourceDownloadState.Observing
        );
        var pending = await AddDownloadAsync(
            automation,
            "Pending.Release",
            RemoteSourceDownloadState.Pending
        );

        // Act
        await service.DeleteAsync(id);

        // Assert
        var readContext = CreateDbContext();
        (await readContext.RemoteSourceAutomations.CountAsync()).ShouldBe(0);
        var remaining = await readContext.RemoteSourceDownloads.SingleAsync();
        remaining.Id.ShouldBe(pending.Id);
        remaining.RemoteSourceAutomationId.ShouldBeNull();
        remaining.RemoteSourceRegistrationId.ShouldBe(automation.RemoteSourceRegistrationId);
    }

    [Test]
    public async Task GetAllAsync_Automations_ReturnsReadModelsSortedByRegistrationAndPriority()
    {
        // Arrange
        var betaRegistration = await AddRegistrationAsync("Beta FTP");
        var alphaRegistration = await AddRegistrationAsync("Alpha FTP");
        var managedTemplate = await AddReleaseTemplateAsync("Managed", ReleaseType.Managed);
        var unmanagedTemplate = await AddReleaseTemplateAsync("Unmanaged", ReleaseType.Unmanaged);
        await service.CreateAsync(
            CreateInput(betaRegistration, managedTemplate) with
            {
                Name = "Beta",
                Priority = 1,
            }
        );
        var catchAllId = await service.CreateAsync(
            CreateInput(alphaRegistration, unmanagedTemplate) with
            {
                Name = "Catch all",
                Priority = 200,
            }
        );
        var germanId = await service.CreateAsync(
            CreateInput(alphaRegistration, managedTemplate) with
            {
                Name = "German",
                FolderNamePattern = "*.German.*",
                PrimaryLanguageCode = "de",
                KeepRawFiles = false,
                Priority = 100,
            }
        );
        var german = await ReloadAsync(germanId);
        await AddDownloadAsync(german, "First.Release", RemoteSourceDownloadState.Observing);
        await AddDownloadAsync(german, "Second.Release", RemoteSourceDownloadState.Observing);
        await AddDownloadAsync(german, "Third.Release", RemoteSourceDownloadState.Ignored);

        // Act
        var automations = await repository.GetAllAsync();

        // Assert
        automations.Select(a => a.Name).ShouldBe(["German", "Catch all", "Beta"]);

        var readModel = automations[0];
        readModel.Id.ShouldBe(germanId);
        readModel.RemoteSourceRegistrationName.ShouldBe("Alpha FTP");
        readModel.IsRemoteSourceRegistrationActive.ShouldBeTrue();
        readModel.RemotePath.ShouldBe("/incoming");
        readModel.TargetPath.ShouldBe(TargetPath);
        readModel.FolderNamePattern.ShouldBe("*.German.*");
        readModel.ReleaseTemplateId.ShouldBe(managedTemplate.Id);
        readModel.ReleaseTemplateName.ShouldBe("Managed");
        readModel.ReleaseType.ShouldBe(ReleaseType.Managed);
        readModel.PrimaryLanguageCode.ShouldBe("de");
        readModel.KeepRawFiles.ShouldBeFalse();
        readModel.Priority.ShouldBe(100);
        readModel.IsEnabled.ShouldBeTrue();
        readModel.DownloadCountsByState[RemoteSourceDownloadState.Observing].ShouldBe(2);
        readModel.DownloadCountsByState[RemoteSourceDownloadState.Ignored].ShouldBe(1);
        readModel.DownloadCountsByState.ShouldNotContainKey(RemoteSourceDownloadState.Pending);

        var catchAll = automations[1];
        catchAll.Id.ShouldBe(catchAllId);
        catchAll.FolderNamePattern.ShouldBeNull();
        catchAll.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        catchAll.DownloadCountsByState.ShouldBeEmpty();
    }

    private async Task<int> CreateAutomationAsync()
    {
        var registration = await AddRegistrationAsync("Main FTP");
        var template = await AddReleaseTemplateAsync("Managed", ReleaseType.Managed);

        return await service.CreateAsync(CreateInput(registration, template));
    }

    private static RemoteSourceAutomationInput CreateInput(
        RemoteSourceRegistration registration,
        ReleaseTemplate template
    )
    {
        return new RemoteSourceAutomationInput
        {
            Name = "Scene TV",
            RemoteSourceRegistrationId = registration.Id,
            RemotePath = "/incoming",
            TargetPath = TargetPath,
            ReleaseTemplateId = template.Id,
        };
    }

    private async Task<RemoteSourceAutomation> ReloadAsync(int id)
    {
        return await CreateDbContext()
            .RemoteSourceAutomations.SingleAsync(automation => automation.Id == id);
    }

    private async Task MarkInitialScanCompletedAsync(int id)
    {
        var context = CreateDbContext();
        var automation = await context.RemoteSourceAutomations.SingleAsync(a => a.Id == id);
        automation.HasCompletedInitialScan = true;
        await context.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private async Task<RemoteSourceRegistration> AddRegistrationAsync(string name)
    {
        var registration = new RemoteSourceRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            SourceClassName = "FakeRemoteSource",
            IsActive = true,
        };

        var context = CreateDbContext();
        context.RemoteSourceRegistrations.Add(registration);
        await context.SaveChangesAsync();

        return registration;
    }

    private async Task<ReleaseTemplate> AddReleaseTemplateAsync(
        string name,
        ReleaseType releaseType
    )
    {
        var template = new ReleaseTemplate
        {
            Name = name,
            ReleaseType = releaseType,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        var context = CreateDbContext();
        context.ReleaseTemplates.Add(template);
        await context.SaveChangesAsync();

        return template;
    }

    private async Task<RemoteSourceDownload> AddDownloadAsync(
        RemoteSourceAutomation automation,
        string folderName,
        RemoteSourceDownloadState state
    )
    {
        var download = new RemoteSourceDownload
        {
            RemoteSourceAutomationId = automation.Id,
            RemoteSourceRegistrationId = automation.RemoteSourceRegistrationId,
            SourceName = "Main FTP",
            RemoteFolderPath = $"{automation.RemotePath}/{folderName}",
            FolderName = folderName,
            LocalFolderPath = Path.Combine(TargetPath, folderName),
            State = state,
            LastChangedAt = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Unspecified),
            DiscoveredAt = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Unspecified),
        };

        var context = CreateDbContext();
        context.RemoteSourceDownloads.Add(download);
        await context.SaveChangesAsync();

        return download;
    }
}
