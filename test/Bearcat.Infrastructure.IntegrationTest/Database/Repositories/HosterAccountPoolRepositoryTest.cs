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
                    false,
                    1
                ),
            ]);
        result[1].MembersInPriorityOrder.ShouldBeEmpty();
        result[2]
            .MembersInPriorityOrder.ShouldBe([
                new HosterAccountPoolMemberReadModel(
                    second.Id,
                    "Second account",
                    false,
                    true,
                    false,
                    1
                ),
                new HosterAccountPoolMemberReadModel(
                    first.Id,
                    "First account",
                    true,
                    false,
                    false,
                    2
                ),
            ]);
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
