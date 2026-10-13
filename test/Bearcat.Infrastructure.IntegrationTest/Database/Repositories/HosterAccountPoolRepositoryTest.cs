using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class HosterAccountPoolRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string RapidgatorClassName = "Rapidgator";
    private const string AlfafileClassName = "Alfafile";

    [Test]
    public async Task GetAllAsync_SeveralPoolsWithMembers_ReturnsPoolsOrderedByNameWithMembersInPriorityOrder()
    {
        // Arrange
        var first = CreateRegistration("First account", RapidgatorClassName);
        var second = CreateRegistration("Second account", RapidgatorClassName, isActive: false);
        second.HasUnreadableSecrets = true;
        var alfafile = CreateRegistration("Alfafile account", AlfafileClassName);
        DbContext.HosterRegistrations.AddRange(first, second, alfafile);
        await DbContext.SaveChangesAsync();

        var rapidgatorPool = CreatePool(
            "Rapidgator pool",
            RapidgatorClassName,
            (second, 1),
            (first, 2)
        );
        rapidgatorPool.MinimumFreeSpaceBytes = 5_000_000_000;
        var alfafilePool = CreatePool("Alfafile pool", AlfafileClassName, (alfafile, 1));
        var emptyPool = CreatePool("Empty pool", RapidgatorClassName);
        DbContext.HosterAccountPools.AddRange(rapidgatorPool, alfafilePool, emptyPool);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await CreateRepository().GetAllAsync(CancellationToken.None);

        // Assert
        result
            .Select(pool =>
                (
                    pool.Id,
                    pool.Name,
                    pool.HosterClassName,
                    pool.HosterName,
                    pool.MinimumFreeSpaceBytes
                )
            )
            .ShouldBe([
                (alfafilePool.Id, "Alfafile pool", AlfafileClassName, "Alfafile hoster", 0L),
                (emptyPool.Id, "Empty pool", RapidgatorClassName, "Rapidgator hoster", 0L),
                (
                    rapidgatorPool.Id,
                    "Rapidgator pool",
                    RapidgatorClassName,
                    "Rapidgator hoster",
                    5_000_000_000L
                ),
            ]);
        result[0]
            .MembersInPriorityOrder.ShouldBe([
                new HosterAccountPoolMemberReadModel(
                    alfafile.Id,
                    "Alfafile account",
                    true,
                    false,
                    1
                ),
            ]);
        result[1].MembersInPriorityOrder.ShouldBeEmpty();
        result[2]
            .MembersInPriorityOrder.ShouldBe([
                new HosterAccountPoolMemberReadModel(second.Id, "Second account", false, true, 1),
                new HosterAccountPoolMemberReadModel(first.Id, "First account", true, false, 2),
            ]);
    }

    [Test]
    public async Task GetDetailAsync_ExistingPool_ReturnsPoolWithMembersInPriorityOrder()
    {
        // Arrange
        var first = CreateRegistration("First account", RapidgatorClassName);
        var second = CreateRegistration("Second account", RapidgatorClassName);
        DbContext.HosterRegistrations.AddRange(first, second);
        await DbContext.SaveChangesAsync();
        var pool = CreatePool("Rapidgator pool", RapidgatorClassName, (first, 2), (second, 1));
        DbContext.HosterAccountPools.AddRange(
            pool,
            CreatePool("Other pool", RapidgatorClassName, (first, 1))
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await CreateRepository().GetDetailAsync(pool.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Rapidgator pool");
        result.HosterName.ShouldBe("Rapidgator hoster");
        result
            .MembersInPriorityOrder.Select(member => member.HosterRegistrationId)
            .ShouldBe([second.Id, first.Id]);
    }

    [Test]
    public async Task GetDetailAsync_UnknownPool_ReturnsNull()
    {
        // Act
        var result = await CreateRepository().GetDetailAsync(42, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetCandidateRegistrationsAsync_RegistrationsOfSeveralHosters_ReturnsRegistrationsOfHosterOrderedByName()
    {
        // Arrange
        var beta = CreateRegistration("Beta account", RapidgatorClassName, isActive: false);
        beta.HasUnreadableSecrets = true;
        var alpha = CreateRegistration("Alpha account", RapidgatorClassName);
        DbContext.HosterRegistrations.AddRange(
            beta,
            alpha,
            CreateRegistration("Alfafile account", AlfafileClassName)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await CreateRepository()
            .GetCandidateRegistrationsAsync(RapidgatorClassName, CancellationToken.None);

        // Assert
        result.ShouldBe([
            new HosterAccountPoolCandidateReadModel(alpha.Id, "Alpha account", true, false),
            new HosterAccountPoolCandidateReadModel(beta.Id, "Beta account", false, true),
        ]);
    }

    [Test]
    public async Task GetHosterClassNamesWithRegistrationsAsync_SeveralRegistrationsPerHoster_ReturnsDistinctClassNames()
    {
        // Arrange
        DbContext.HosterRegistrations.AddRange(
            CreateRegistration("First account", RapidgatorClassName),
            CreateRegistration("Second account", RapidgatorClassName),
            CreateRegistration("Alfafile account", AlfafileClassName)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await CreateRepository()
            .GetHosterClassNamesWithRegistrationsAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([AlfafileClassName, RapidgatorClassName]);
    }

    [Test]
    public async Task GetHosterClassNamesWithRegistrationsAsync_NoRegistrations_ReturnsEmptyList()
    {
        // Act
        var result = await CreateRepository()
            .GetHosterClassNamesWithRegistrationsAsync(CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetByIdAsync_ExistingPool_ReturnsPoolWithMembers()
    {
        // Arrange
        var registration = CreateRegistration("First account", RapidgatorClassName);
        DbContext.HosterRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();
        var pool = CreatePool("Rapidgator pool", RapidgatorClassName, (registration, 1));
        DbContext.HosterAccountPools.Add(pool);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await CreateRepository().GetByIdAsync(pool.Id, CancellationToken.None);

        // Assert
        result.Name.ShouldBe("Rapidgator pool");
        result.Members.Select(member => member.HosterRegistrationId).ShouldBe([registration.Id]);
    }

    [TestCase("Rapidgator pool", true)]
    [TestCase("Other pool", false)]
    public async Task NameExistsAsync_ExistingPool_ReturnsWhetherNameIsUsed(
        string name,
        bool expectedResult
    )
    {
        // Arrange
        DbContext.HosterAccountPools.Add(CreatePool("Rapidgator pool", RapidgatorClassName));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await CreateRepository()
            .NameExistsAsync(name, excludedHosterAccountPoolId: null, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Test]
    public async Task NameExistsAsync_NameOnlyUsedByExcludedPool_ReturnsFalse()
    {
        // Arrange
        var pool = CreatePool("Rapidgator pool", RapidgatorClassName);
        DbContext.HosterAccountPools.Add(pool);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await CreateRepository()
            .NameExistsAsync("Rapidgator pool", pool.Id, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task GetHosterClassNamesByHosterRegistrationIdAsync_RequestedRegistrations_ReturnsClassNamesOfRequestedRegistrations()
    {
        // Arrange
        var rapidgator = CreateRegistration("Rapidgator account", RapidgatorClassName);
        var alfafile = CreateRegistration("Alfafile account", AlfafileClassName);
        var notRequested = CreateRegistration("Other account", RapidgatorClassName);
        DbContext.HosterRegistrations.AddRange(rapidgator, alfafile, notRequested);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await CreateRepository()
            .GetHosterClassNamesByHosterRegistrationIdAsync(
                [rapidgator.Id, alfafile.Id],
                CancellationToken.None
            );

        // Assert
        result.ShouldBe(
            new Dictionary<int, string>
            {
                [rapidgator.Id] = RapidgatorClassName,
                [alfafile.Id] = AlfafileClassName,
            },
            ignoreOrder: true
        );
    }

    [Test]
    public async Task Remove_PoolWithMembers_DeletesPoolAndMembersButKeepsRegistrations()
    {
        // Arrange
        var registration = CreateRegistration("First account", RapidgatorClassName);
        DbContext.HosterRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();
        var pool = CreatePool("Rapidgator pool", RapidgatorClassName, (registration, 1));
        DbContext.HosterAccountPools.Add(pool);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        var repository = CreateRepository();

        // Act
        repository.Remove(await repository.GetByIdAsync(pool.Id, CancellationToken.None));
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var dbContext = CreateDbContext();
        (await dbContext.HosterAccountPools.AnyAsync()).ShouldBeFalse();
        (await dbContext.HosterAccountPoolMembers.AnyAsync()).ShouldBeFalse();
        (await dbContext.HosterRegistrations.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task SaveChangesAsync_RemovedRegistrationIsPoolMember_DeletesMembershipButKeepsPool()
    {
        // Arrange
        var removed = CreateRegistration("Removed account", RapidgatorClassName);
        var kept = CreateRegistration("Kept account", RapidgatorClassName);
        DbContext.HosterRegistrations.AddRange(removed, kept);
        await DbContext.SaveChangesAsync();
        DbContext.HosterAccountPools.Add(
            CreatePool("Rapidgator pool", RapidgatorClassName, (removed, 1), (kept, 2))
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        var dbContext = CreateDbContext();

        // Act
        dbContext.HosterRegistrations.Remove(
            await dbContext.HosterRegistrations.SingleAsync(registration =>
                registration.Id == removed.Id
            )
        );
        await dbContext.SaveChangesAsync();

        // Assert
        var members = await CreateDbContext().HosterAccountPoolMembers.ToListAsync();
        members.Select(member => member.HosterRegistrationId).ShouldBe([kept.Id]);
    }

    [Test]
    public async Task SaveChangesAsync_DuplicateName_ThrowsDbUpdateException()
    {
        // Arrange
        DbContext.HosterAccountPools.Add(CreatePool("Rapidgator pool", RapidgatorClassName));
        await DbContext.SaveChangesAsync();
        var repository = CreateRepository();
        repository.Add(CreatePool("Rapidgator pool", AlfafileClassName));

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
    }

    [Test]
    public async Task SaveChangesAsync_DuplicateMember_ThrowsDbUpdateException()
    {
        // Arrange
        var registration = CreateRegistration("First account", RapidgatorClassName);
        DbContext.HosterRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();
        var repository = CreateRepository();
        repository.Add(
            CreatePool("Rapidgator pool", RapidgatorClassName, (registration, 1), (registration, 2))
        );

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
    }

    private HosterAccountPoolRepository CreateRepository()
    {
        var hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactoryMock
            .Setup(factory => factory.GetByName(RapidgatorClassName))
            .Returns(CreateHoster("Rapidgator hoster"));
        hosterFactoryMock
            .Setup(factory => factory.GetByName(AlfafileClassName))
            .Returns(CreateHoster("Alfafile hoster"));

        return new HosterAccountPoolRepository(
            ReadDbContext,
            CreateDbContext(),
            hosterFactoryMock.Object
        );
    }

    private static IHoster CreateHoster(string name)
    {
        var hosterMock = new Mock<IHoster>();
        hosterMock.Setup(hoster => hoster.Name).Returns(name);

        return hosterMock.Object;
    }

    private static HosterRegistration CreateRegistration(
        string name,
        string hosterClassName,
        bool isActive = true
    )
    {
        return new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
            IsActive = isActive,
        };
    }

    private static HosterAccountPool CreatePool(
        string name,
        string hosterClassName,
        params (HosterRegistration Registration, int Priority)[] members
    )
    {
        return new HosterAccountPool
        {
            Name = name,
            HosterClassName = hosterClassName,
            Members = members
                .Select(member => new HosterAccountPoolMember
                {
                    HosterRegistrationId = member.Registration.Id,
                    Priority = member.Priority,
                })
                .ToList(),
        };
    }
}
