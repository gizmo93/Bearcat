using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageHosterAccountPools;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Deletion;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Dto;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Validation;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageHosterAccountPools;

public class HosterAccountPoolServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string StorageQuotaHosterClassName = "StorageQuotaHoster";
    private const string OtherStorageQuotaHosterClassName = "OtherStorageQuotaHoster";
    private const string PlainHosterClassName = "PlainHoster";
    private const string UnregisteredStorageQuotaHosterClassName = "UnregisteredHoster";

    private HosterAccountPoolService service = null!;

    [SetUp]
    public void Setup()
    {
        var hostersByName = new Dictionary<string, IHoster>
        {
            [StorageQuotaHosterClassName] = CreateStorageQuotaHoster("Storage hoster"),
            [OtherStorageQuotaHosterClassName] = CreateStorageQuotaHoster("Another hoster"),
            [UnregisteredStorageQuotaHosterClassName] = CreateStorageQuotaHoster(
                "Unregistered hoster"
            ),
            [PlainHosterClassName] = CreatePlainHoster("Plain hoster"),
        };
        var hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactoryMock.Setup(factory => factory.GetHostersByName()).Returns(hostersByName);
        hosterFactoryMock
            .Setup(factory => factory.GetByName(It.IsAny<string>()))
            .Returns((string hosterClassName) => hostersByName[hosterClassName]);

        var repository = new HosterAccountPoolRepository(
            DbContext,
            DbContext,
            hosterFactoryMock.Object
        );
        service = new HosterAccountPoolService(repository, repository, hosterFactoryMock.Object);
    }

    [Test]
    public async Task CreateAsync_ValidInput_PersistsTrimmedNameReserveAndMemberPrioritiesFromOrder()
    {
        // Arrange
        var first = await AddRegistrationAsync("First account", StorageQuotaHosterClassName);
        var second = await AddRegistrationAsync("Second account", StorageQuotaHosterClassName);
        var third = await AddRegistrationAsync("Third account", StorageQuotaHosterClassName);

        // Act
        var result = await service.CreateAsync(
            StorageQuotaHosterClassName,
            new HosterAccountPoolInput(
                "  Main pool  ",
                1_073_741_824,
                [third.Id, first.Id, second.Id]
            ),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var pool = await CreateDbContext()
            .HosterAccountPools.Include(pool => pool.Members)
            .SingleAsync(pool => pool.Id == result.HosterAccountPoolId);
        pool.Name.ShouldBe("Main pool");
        pool.HosterClassName.ShouldBe(StorageQuotaHosterClassName);
        pool.MinimumFreeSpaceBytes.ShouldBe(1_073_741_824);
        pool.Members.OrderBy(member => member.Priority)
            .Select(member => (member.HosterRegistrationId, member.Priority))
            .ShouldBe([(third.Id, 1), (first.Id, 2), (second.Id, 3)]);
    }

    [Test]
    public async Task CreateAsync_HosterWithoutStorageQuota_ThrowsInvalidOperationException()
    {
        // Arrange
        var registration = await AddRegistrationAsync("Plain account", PlainHosterClassName);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                PlainHosterClassName,
                new HosterAccountPoolInput("Plain pool", 0, [registration.Id]),
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("Plain hoster");
        (await CreateDbContext().HosterAccountPools.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_MemberOfOtherHoster_ThrowsInvalidOperationException()
    {
        // Arrange
        var registration = await AddRegistrationAsync(
            "Other account",
            OtherStorageQuotaHosterClassName
        );

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                StorageQuotaHosterClassName,
                new HosterAccountPoolInput("Main pool", 0, [registration.Id]),
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain(OtherStorageQuotaHosterClassName);
        (await CreateDbContext().HosterAccountPools.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_DuplicateMember_ThrowsArgumentException()
    {
        // Arrange
        var registration = await AddRegistrationAsync("First account", StorageQuotaHosterClassName);

        // Act
        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            service.CreateAsync(
                StorageQuotaHosterClassName,
                new HosterAccountPoolInput("Main pool", 0, [registration.Id, registration.Id]),
                CancellationToken.None
            )
        );

        // Assert
        exception.ShouldNotBeNull();
        (await CreateDbContext().HosterAccountPools.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_NameOfExistingPool_ReturnsNameAlreadyExists()
    {
        // Arrange
        await service.CreateAsync(
            StorageQuotaHosterClassName,
            new HosterAccountPoolInput("Main pool", 0, []),
            CancellationToken.None
        );

        // Act
        var result = await service.CreateAsync(
            OtherStorageQuotaHosterClassName,
            new HosterAccountPoolInput(" Main pool ", 0, []),
            CancellationToken.None
        );

        // Assert
        result.ValidationErrors.ShouldBe([HosterAccountPoolValidationError.NameAlreadyExists]);
        (await CreateDbContext().HosterAccountPools.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task CreateAsync_EmptyNameAndNegativeReserve_ReturnsBothValidationErrors()
    {
        // Act
        var result = await service.CreateAsync(
            StorageQuotaHosterClassName,
            new HosterAccountPoolInput("   ", -1, []),
            CancellationToken.None
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            HosterAccountPoolValidationError.NameRequired,
            HosterAccountPoolValidationError.MinimumFreeSpaceNegative,
        ]);
    }

    [Test]
    public async Task CreateAsync_NameLongerThanMaximum_ReturnsNameTooLong()
    {
        // Act
        var result = await service.CreateAsync(
            StorageQuotaHosterClassName,
            new HosterAccountPoolInput(
                new string('a', HosterAccountPoolService.NameMaxLength + 1),
                0,
                []
            ),
            CancellationToken.None
        );

        // Assert
        result.ValidationErrors.ShouldBe([HosterAccountPoolValidationError.NameTooLong]);
    }

    [Test]
    public async Task UpdateAsync_ReorderedRemovedAndAddedMembers_PersistsNewOrderAndKeepsExistingMemberRows()
    {
        // Arrange
        var first = await AddRegistrationAsync("First account", StorageQuotaHosterClassName);
        var second = await AddRegistrationAsync("Second account", StorageQuotaHosterClassName);
        var third = await AddRegistrationAsync("Third account", StorageQuotaHosterClassName);
        var created = await service.CreateAsync(
            StorageQuotaHosterClassName,
            new HosterAccountPoolInput("Main pool", 0, [first.Id, second.Id]),
            CancellationToken.None
        );
        var poolId = created.HosterAccountPoolId!.Value;
        var secondMemberId = (
            await CreateDbContext()
                .HosterAccountPoolMembers.SingleAsync(member =>
                    member.HosterRegistrationId == second.Id
                )
        ).Id;

        // Act
        var result = await service.UpdateAsync(
            poolId,
            new HosterAccountPoolInput("Renamed pool", 2_147_483_648, [third.Id, second.Id]),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var pool = await CreateDbContext()
            .HosterAccountPools.Include(pool => pool.Members)
            .SingleAsync(pool => pool.Id == poolId);
        pool.Name.ShouldBe("Renamed pool");
        pool.MinimumFreeSpaceBytes.ShouldBe(2_147_483_648);
        pool.Members.OrderBy(member => member.Priority)
            .Select(member => (member.HosterRegistrationId, member.Priority))
            .ShouldBe([(third.Id, 1), (second.Id, 2)]);
        pool.Members.Single(member => member.HosterRegistrationId == second.Id)
            .Id.ShouldBe(secondMemberId);
    }

    [Test]
    public async Task UpdateAsync_KeepsOwnName_Succeeds()
    {
        // Arrange
        var created = await service.CreateAsync(
            StorageQuotaHosterClassName,
            new HosterAccountPoolInput("Main pool", 0, []),
            CancellationToken.None
        );

        // Act
        var result = await service.UpdateAsync(
            created.HosterAccountPoolId!.Value,
            new HosterAccountPoolInput("Main pool", 1, []),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_MemberOfOtherHoster_ThrowsInvalidOperationException()
    {
        // Arrange
        var created = await service.CreateAsync(
            StorageQuotaHosterClassName,
            new HosterAccountPoolInput("Main pool", 0, []),
            CancellationToken.None
        );
        var otherRegistration = await AddRegistrationAsync(
            "Other account",
            OtherStorageQuotaHosterClassName
        );

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.UpdateAsync(
                created.HosterAccountPoolId!.Value,
                new HosterAccountPoolInput("Main pool", 0, [otherRegistration.Id]),
                CancellationToken.None
            )
        );

        // Assert
        exception.ShouldNotBeNull();
        (await CreateDbContext().HosterAccountPoolMembers.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task DeleteAsync_PoolWithMembers_DeletesPoolAndMembers()
    {
        // Arrange
        var registration = await AddRegistrationAsync("First account", StorageQuotaHosterClassName);
        var created = await service.CreateAsync(
            StorageQuotaHosterClassName,
            new HosterAccountPoolInput("Main pool", 0, [registration.Id]),
            CancellationToken.None
        );

        // Act
        var result = await service.DeleteAsync(
            created.HosterAccountPoolId!.Value,
            CancellationToken.None
        );

        // Assert
        var dbContext = CreateDbContext();
        result.IsDeleted.ShouldBeTrue();
        (await dbContext.HosterAccountPools.AnyAsync()).ShouldBeFalse();
        (await dbContext.HosterAccountPoolMembers.AnyAsync()).ShouldBeFalse();
        (await dbContext.HosterRegistrations.AnyAsync()).ShouldBeTrue();
    }

    [Test]
    public async Task DeleteAsync_PoolUsedByUploadConfig_KeepsPoolAndReturnsUsage()
    {
        // Arrange
        var registration = await AddRegistrationAsync("First account", StorageQuotaHosterClassName);
        var created = await service.CreateAsync(
            StorageQuotaHosterClassName,
            new HosterAccountPoolInput("Main pool", 0, [registration.Id]),
            CancellationToken.None
        );
        var release = new Release
        {
            Name = "Bearcat.Release",
            ReleaseType = ReleaseType.Unmanaged,
            ReleaseGroup = new ReleaseGroup
            {
                Name = "Group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        DbContext.UploadConfigs.Add(
            new UploadConfig
            {
                UploadSource = UploadSource.ArchiveFiles,
                Release = release,
                ArchiveConfig = new ArchiveConfig
                {
                    Release = release,
                    Name = "Main archive",
                    ArchiveFilesBasePath = "/archives",
                    ArchiverName = "rar",
                    ArchiveFileSizeMb = 512,
                },
                HosterAccountPoolId = created.HosterAccountPoolId,
                Name = "Pool upload",
            }
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.DeleteAsync(
            created.HosterAccountPoolId!.Value,
            CancellationToken.None
        );

        // Assert
        result.IsDeleted.ShouldBeFalse();
        result.Usage.ShouldBe(new HosterAccountPoolUsage(1, 0));
        (await CreateDbContext().HosterAccountPools.AnyAsync()).ShouldBeTrue();
    }

    [Test]
    public async Task GetPoolableHosterClassesAsync_RegisteredHosters_ReturnsOnlyRegisteredStorageQuotaHostersOrderedByName()
    {
        // Arrange
        await AddRegistrationAsync("First account", StorageQuotaHosterClassName);
        await AddRegistrationAsync("Second account", StorageQuotaHosterClassName);
        await AddRegistrationAsync("Other account", OtherStorageQuotaHosterClassName);
        await AddRegistrationAsync("Plain account", PlainHosterClassName);

        // Act
        var result = await service.GetPoolableHosterClassesAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new PoolableHosterClassReadModel(OtherStorageQuotaHosterClassName, "Another hoster"),
            new PoolableHosterClassReadModel(StorageQuotaHosterClassName, "Storage hoster"),
        ]);
    }

    private async Task<HosterRegistration> AddRegistrationAsync(string name, string hosterClassName)
    {
        var registration = new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
            IsActive = true,
        };
        DbContext.HosterRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();

        return registration;
    }

    private static IHoster CreateStorageQuotaHoster(string name)
    {
        var hosterMock = new Mock<IHosterWithStorageQuota>();
        hosterMock.Setup(hoster => hoster.Name).Returns(name);

        return hosterMock.Object;
    }

    private static IHoster CreatePlainHoster(string name)
    {
        var hosterMock = new Mock<IHoster>();
        hosterMock.Setup(hoster => hoster.Name).Returns(name);

        return hosterMock.Object;
    }
}
