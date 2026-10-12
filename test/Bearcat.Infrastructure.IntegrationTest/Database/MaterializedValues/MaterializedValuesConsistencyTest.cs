using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;

public class MaterializedValuesConsistencyTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const int ReleaseCount = 12;
    private const int ChangeRoundCount = 6;

    private static readonly OnlineState[] UploadOnlineStates =
    [
        OnlineState.Unknown,
        OnlineState.Online,
        OnlineState.PartiallyOnline,
        OnlineState.Offline,
    ];

    private readonly Random random = new(20261012);

    private MaterializedValuesTestData testData = null!;

    [SetUp]
    public void Setup()
    {
        testData = new MaterializedValuesTestData(DbContext);
    }

    [Test]
    public async Task SaveChangesAsync_VariedChangesInSeveralSaves_StoredValuesEqualValuesComputedFromUploadConfigsUploadsAndUploadedFiles()
    {
        // Arrange
        var hosterRegistrations = new List<HosterRegistration>
        {
            testData.AddHosterRegistration("Alpha"),
            testData.AddHosterRegistration("Beta"),
            testData.AddHosterRegistration("Inactive", isActive: false),
        };
        var releases = Enumerable
            .Range(1, ReleaseCount)
            .Select(number => testData.AddRelease($"Release {number}"))
            .ToList();
        foreach (var release in releases)
        {
            var uploadConfigCount = random.Next(0, 4);
            for (var configNumber = 0; configNumber < uploadConfigCount; configNumber++)
            {
                AddUploadConfigWithRandomUploads(release, PickRandom(hosterRegistrations));
            }
        }

        // Act
        await DbContext.SaveChangesAsync();
        for (var round = 0; round < ChangeRoundCount; round++)
        {
            await ApplyRandomChangesAsync(releases, hosterRegistrations);
            await DbContext.SaveChangesAsync();
        }

        // Assert
        await AssertStoredValuesEqualComputedValuesAsync();
    }

    private async Task ApplyRandomChangesAsync(
        List<Release> releases,
        List<HosterRegistration> hosterRegistrations
    )
    {
        var uploadConfigs = await DbContext
            .UploadConfigs.Include(uploadConfig => uploadConfig.ArchiveConfig)
            .Include(uploadConfig => uploadConfig.Uploads)
                .ThenInclude(upload => upload.UploadedFiles)
            .OrderBy(uploadConfig => uploadConfig.Id)
            .ToListAsync();
        var uploads = uploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads).ToList();
        var uploadedFiles = uploads.SelectMany(upload => upload.UploadedFiles).ToList();

        foreach (var upload in uploads.Where(_ => random.Next(3) == 0))
        {
            upload.OnlineState = PickRandom(UploadOnlineStates);
        }

        foreach (var uploadedFile in uploadedFiles.Where(_ => random.Next(3) == 0))
        {
            uploadedFile.CheckedAt = CreateRandomCheckedAt();
        }

        if (uploadedFiles.Count > 0)
        {
            DbContext.UploadedFiles.Remove(PickRandom(uploadedFiles));
        }

        if (uploads.Count > 1)
        {
            var movedUpload = PickRandom(uploads);
            movedUpload.UploadConfig = PickRandom(uploadConfigs);
        }

        if (uploads.Count > 0)
        {
            DbContext.Uploads.Remove(PickRandom(uploads));
        }

        if (uploadConfigs.Count > 1)
        {
            PickRandom(uploadConfigs).Release = PickRandom(releases);
            DbContext.UploadConfigs.Remove(PickRandom(uploadConfigs));
        }

        AddUploadConfigWithRandomUploads(PickRandom(releases), PickRandom(hosterRegistrations));
    }

    private void AddUploadConfigWithRandomUploads(
        Release release,
        HosterRegistration hosterRegistration
    )
    {
        var uploadConfig = testData.AddUploadConfig(release, hosterRegistration);
        var uploadCount = random.Next(0, 4);
        for (var uploadNumber = 0; uploadNumber < uploadCount; uploadNumber++)
        {
            var fileCheckedAts = Enumerable
                .Range(0, random.Next(0, 4))
                .Select(_ => CreateRandomCheckedAt())
                .ToArray();
            testData.AddUpload(uploadConfig, PickRandom(UploadOnlineStates), fileCheckedAts);
        }
    }

    private DateTime? CreateRandomCheckedAt()
    {
        return random.Next(4) == 0
            ? null
            : MaterializedValuesTestData.LocalNow.AddMinutes(-random.Next(0, 600));
    }

    private T PickRandom<T>(IReadOnlyList<T> items)
    {
        return items[random.Next(items.Count)];
    }

    private async Task AssertStoredValuesEqualComputedValuesAsync()
    {
        var releases = await ReadDbContext
            .Releases.Select(release => new { release.Id, release.OnlineState })
            .ToListAsync();
        var uploadConfigs = await ReadDbContext
            .UploadConfigs.Select(uploadConfig => new { uploadConfig.Id, uploadConfig.ReleaseId })
            .ToListAsync();
        var uploads = await ReadDbContext
            .Uploads.Select(upload => new
            {
                upload.Id,
                upload.UploadConfigId,
                upload.OnlineState,
                upload.OldestUploadedFileCheckedAt,
            })
            .ToListAsync();
        var uploadedFiles = await ReadDbContext
            .UploadedFiles.Select(uploadedFile => new
            {
                uploadedFile.UploadId,
                uploadedFile.CheckedAt,
            })
            .ToListAsync();

        uploadedFiles.ShouldNotBeEmpty();
        releases.Select(release => release.OnlineState).Distinct().Count().ShouldBeGreaterThan(2);

        foreach (var release in releases)
        {
            var uploadConfigIdsOfRelease = uploadConfigs
                .Where(uploadConfig => uploadConfig.ReleaseId == release.Id)
                .Select(uploadConfig => uploadConfig.Id)
                .ToList();
            var onlineUploadConfigCount = uploadConfigIdsOfRelease.Count(uploadConfigId =>
                uploads.Any(upload =>
                    upload.UploadConfigId == uploadConfigId
                    && upload.OnlineState == OnlineState.Online
                )
            );
            var computedOnlineState =
                uploadConfigIdsOfRelease.Count == 0 ? OnlineState.Unknown
                : onlineUploadConfigCount == uploadConfigIdsOfRelease.Count ? OnlineState.Online
                : onlineUploadConfigCount > 0 ? OnlineState.PartiallyOnline
                : OnlineState.Offline;

            release.OnlineState.ShouldBe(computedOnlineState, $"Release {release.Id}");
        }

        foreach (var upload in uploads)
        {
            var checkedAtsOfUploadedFiles = uploadedFiles
                .Where(uploadedFile => uploadedFile.UploadId == upload.Id)
                .Select(uploadedFile => uploadedFile.CheckedAt ?? DateTime.MinValue)
                .ToList();
            DateTime? computedOldestCheckedAt =
                checkedAtsOfUploadedFiles.Count == 0 ? null : checkedAtsOfUploadedFiles.Min();

            upload.OldestUploadedFileCheckedAt.ShouldBe(
                computedOldestCheckedAt,
                $"Upload {upload.Id}"
            );
        }
    }
}
