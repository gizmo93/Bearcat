using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseFolderRetirementRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime Cutoff = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private ReleaseFolderRetirementRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseFolderRetirementRepository(DbContext);
    }

    [Test]
    public async Task GetConversionCandidatesAsync_OverdueManagedRelease_LoadsArchiveFiles()
    {
        // Arrange
        var release = await AddReleaseAsync(
            "Overdue",
            ReleaseType.Managed,
            uploadsPostedAt: Cutoff.AddDays(-1)
        );

        // Act
        var result = await GetConversionCandidatesAsync();

        // Assert
        var candidate = result.ShouldHaveSingleItem();
        candidate.Id.ShouldBe(release.Id);
        candidate
            .ArchiveConfigs.ShouldHaveSingleItem()
            .Archives.ShouldHaveSingleItem()
            .ArchiveFiles.Select(f => f.FullFileName)
            .ShouldBe(
                ["/archives/overdue.part1.rar", "/archives/overdue.part2.rar"],
                ignoreOrder: true
            );
    }

    [Test]
    public async Task GetConversionCandidatesAsync_ReleaseIsNotOverdueOrNotManaged_ReturnsNoCandidates()
    {
        // Arrange
        await AddReleaseAsync("Recent", ReleaseType.Managed, uploadsPostedAt: Cutoff.AddDays(1));
        await AddReleaseAsync(
            "Unmanaged",
            ReleaseType.Unmanaged,
            uploadsPostedAt: Cutoff.AddDays(-1)
        );

        // Act
        var result = await GetConversionCandidatesAsync();

        // Assert
        result.ShouldBeEmpty();
    }

    [TestCase(-1, true)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    public async Task GetConversionCandidatesAsync_UploadsPostedRelativeToCutoff_ReturnsReleaseOnlyWhenPostedAtOrBeforeCutoff(
        int postedMinutesAfterCutoff,
        bool expectedToBeReturned
    )
    {
        // Arrange
        await AddReleaseAsync(
            "Release",
            ReleaseType.Managed,
            uploadsPostedAt: Cutoff.AddMinutes(postedMinutesAfterCutoff)
        );

        // Act
        var result = await GetConversionCandidatesAsync();

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [TestCase(-1, true)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    public async Task GetConversionCandidatesAsync_NotPostedAndEarliestUploadRelativeToCutoff_ReturnsReleaseOnlyWhenFirstUploadedAtOrBeforeCutoff(
        int earliestUploadMinutesAfterCutoff,
        bool expectedToBeReturned
    )
    {
        // Arrange
        await AddReleaseAsync(
            "Release",
            ReleaseType.Managed,
            uploadsPostedAt: null,
            uploads:
            [
                (Cutoff.AddMinutes(earliestUploadMinutesAfterCutoff), UploadState.Completed),
                (Cutoff.AddDays(5), UploadState.Completed),
            ]
        );

        // Act
        var result = await GetConversionCandidatesAsync();

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [TestCase(UploadState.WaitingForArchive, false)]
    [TestCase(UploadState.Pending, false)]
    [TestCase(UploadState.Uploading, false)]
    [TestCase(UploadState.Failed, true)]
    public async Task GetConversionCandidatesAsync_OverdueReleaseWithUploadInState_ReturnsReleaseOnlyWithoutActiveUpload(
        UploadState uploadState,
        bool expectedToBeReturned
    )
    {
        // Arrange
        await AddReleaseAsync(
            "Release",
            ReleaseType.Managed,
            uploadsPostedAt: Cutoff.AddDays(-1),
            uploads: [(Cutoff.AddDays(-2), UploadState.Completed), (null, uploadState)]
        );

        // Act
        var result = await GetConversionCandidatesAsync();

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [Test]
    public async Task GetConversionCandidatesAsync_MoreCandidatesThanMaximumCount_ReturnsCandidatesWithLowestIdsAfterGivenReleaseId()
    {
        // Arrange
        var firstRelease = await AddReleaseAsync(
            "First",
            ReleaseType.Managed,
            uploadsPostedAt: Cutoff.AddDays(-1)
        );
        await AddReleaseAsync("Recent", ReleaseType.Managed, uploadsPostedAt: Cutoff.AddDays(1));
        var thirdRelease = await AddReleaseAsync(
            "Third",
            ReleaseType.Managed,
            uploadsPostedAt: Cutoff.AddDays(-1)
        );
        var fourthRelease = await AddReleaseAsync(
            "Fourth",
            ReleaseType.Managed,
            uploadsPostedAt: Cutoff.AddDays(-1)
        );
        await AddReleaseAsync("Fifth", ReleaseType.Managed, uploadsPostedAt: Cutoff.AddDays(-1));

        // Act
        var result = await repository.GetConversionCandidatesAsync(
            Cutoff,
            afterReleaseId: firstRelease.Id,
            maximumReleaseCount: 2,
            CancellationToken.None
        );

        // Assert
        result.Select(release => release.Id).ShouldBe([thirdRelease.Id, fourthRelease.Id]);
    }

    [Test]
    public async Task GetConversionCandidatesAsync_OverdueReleaseWithMirrorRegistration_LoadsUploadsWithUploadedFilesAndHosterRegistration()
    {
        // Arrange
        await AddReleaseAsync(
            "Overdue",
            ReleaseType.Managed,
            uploadsPostedAt: Cutoff.AddDays(-1),
            uploads: [(Cutoff.AddDays(-2), UploadState.Completed)],
            hosterRegistrationIsActive: true,
            hosterRegistrationUseForMirrorDownloads: true
        );

        // Act
        var result = await GetConversionCandidatesAsync();

        // Assert
        var uploadConfig = result
            .ShouldHaveSingleItem()
            .ArchiveConfigs.ShouldHaveSingleItem()
            .UploadConfigs.ShouldHaveSingleItem();
        uploadConfig.HosterRegistration!.Name.ShouldBe("Overdue hoster");
        uploadConfig
            .Uploads.ShouldHaveSingleItem()
            .UploadedFiles.ShouldHaveSingleItem()
            .HosterFileLink.ShouldBe("https://hoster.test/overdue");
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task GetConversionCandidatesAsync_HosterRegistrationNotUsableForMirrorDownloads_LoadsUploadsWithoutUploadedFiles(
        bool hosterRegistrationIsActive,
        bool hosterRegistrationUseForMirrorDownloads
    )
    {
        // Arrange
        await AddReleaseAsync(
            "Overdue",
            ReleaseType.Managed,
            uploadsPostedAt: Cutoff.AddDays(-1),
            uploads: [(Cutoff.AddDays(-2), UploadState.Completed)],
            hosterRegistrationIsActive: hosterRegistrationIsActive,
            hosterRegistrationUseForMirrorDownloads: hosterRegistrationUseForMirrorDownloads
        );

        // Act
        var result = await GetConversionCandidatesAsync();

        // Assert
        result
            .ShouldHaveSingleItem()
            .ArchiveConfigs.ShouldHaveSingleItem()
            .UploadConfigs.ShouldHaveSingleItem()
            .Uploads.ShouldHaveSingleItem()
            .UploadedFiles.ShouldBeEmpty();
    }

    private async Task<IReadOnlyList<Release>> GetConversionCandidatesAsync()
    {
        return await repository.GetConversionCandidatesAsync(
            Cutoff,
            afterReleaseId: 0,
            maximumReleaseCount: 100,
            CancellationToken.None
        );
    }

    private async Task<Release> AddReleaseAsync(
        string name,
        ReleaseType releaseType,
        DateTime? uploadsPostedAt,
        IReadOnlyList<(DateTime? UploadedAt, UploadState UploadState)>? uploads = null,
        bool hosterRegistrationIsActive = true,
        bool hosterRegistrationUseForMirrorDownloads = false
    )
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = releaseType,
            ReleaseFolderPath = releaseType is ReleaseType.Managed ? $"/releases/{name}" : null,
            UploadsPostedAt = uploadsPostedAt,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/archives",
            ArchiverName = "zip",
            ArchiveNamePrefix = name,
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
            Archives =
            [
                new Archive
                {
                    ArchiveFolderPath = "/archives",
                    ArchiveState = ArchiveState.Created,
                    ArchiveFileSizeMb = 512,
                    CreatedAt = Cutoff.AddDays(-30),
                    ArchiveFiles =
                    [
                        new ArchiveFile
                        {
                            FullFileName = $"/archives/{name.ToLowerInvariant()}.part1.rar",
                        },
                        new ArchiveFile
                        {
                            FullFileName = $"/archives/{name.ToLowerInvariant()}.part2.rar",
                        },
                    ],
                    Uploads = [],
                    ErrorMessages = [],
                },
            ],
        };

        var archiveFile = archiveConfig.Archives[0].ArchiveFiles[0];
        var hosterRegistration = new HosterRegistration
        {
            Name = $"{name} hoster",
            SerializedConfig = "{}",
            HosterClassName = "Hoster",
            IsActive = hosterRegistrationIsActive,
            UseForMirrorDownloads = hosterRegistrationUseForMirrorDownloads,
        };
        archiveConfig.UploadConfigs =
        [
            new UploadConfig
            {
                UploadSource = UploadSource.ArchiveFiles,
                Release = release,
                Name = "Default upload",
                HosterRegistration = hosterRegistration,
                Uploads = (uploads ?? [])
                    .Select(upload => new Upload
                    {
                        HosterRegistration = hosterRegistration,
                        CreatedAt = Cutoff.AddDays(-10),
                        UploadedAt = upload.UploadedAt,
                        UploadState = upload.UploadState,
                        OnlineState = OnlineState.Online,
                        ErrorMessages = [],
                        UploadedFiles =
                        [
                            new UploadedFile
                            {
                                ArchiveFile = archiveFile,
                                HosterFileLink = $"https://hoster.test/{name.ToLowerInvariant()}",
                                OnlineState = OnlineState.Online,
                                CreatedAt = Cutoff.AddDays(-10),
                                ErrorMessages = [],
                            },
                        ],
                    })
                    .ToList(),
            },
        ];

        DbContext.ArchiveConfigs.Add(archiveConfig);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return release;
    }
}
