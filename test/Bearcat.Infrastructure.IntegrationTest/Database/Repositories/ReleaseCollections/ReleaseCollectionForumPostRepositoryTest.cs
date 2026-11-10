using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories.ReleaseCollections;

public class ReleaseCollectionForumPostRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime CreatedAt = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ReleaseCollectionForumPostRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseCollectionForumPostRepository(ReadDbContext);
    }

    [Test]
    public async Task GetAsync_CollectionWithUploadSlots_ReturnsOrderedSlotsWithOnlyTheirCollectionContainers()
    {
        // Arrange
        var zCrypter = CreateLinkCrypterRegistration("zcrypt");
        var aCrypter = CreateLinkCrypterRegistration("acrypt");
        var collection = CreateCollection("Bodies");
        var otherCollection = CreateCollection("Other");
        var slotB = CreateUploadSlot(collection, "B Slot", "b");
        var slotA = CreateUploadSlot(collection, "A Slot", "a");
        CreateUploadSlot(collection, "C Slot", "c");
        var otherCollectionSlot = CreateUploadSlot(otherCollection, "A Slot", "a");

        AddContainer(slotA, zCrypter, "https://crypter.test/a-z", password: "zpw");
        AddContainer(slotA, aCrypter, "https://crypter.test/a-a", password: null);
        AddContainer(slotA, aCrypter, string.Empty, password: null);

        AddContainer(
            slot: slotA,
            linkCrypterRegistration: aCrypter,
            containerUrl: "https://crypter.test/other-scope",
            password: null,
            scope: LinkCrypterContainerScope.ReleaseAllHosters
        );

        AddContainer(slotB, zCrypter, "https://crypter.test/b-z", password: null);
        AddContainer(otherCollectionSlot, aCrypter, "https://crypter.test/other", password: null);

        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAsync(collection.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.UploadSlots.Select(slot => slot.Key).ShouldBe(["a", "b", "c"]);

        result
            .UploadSlots[0]
            .LinkCrypters.Select(linkCrypter => (linkCrypter.Name, linkCrypter.ContainerUrl))
            .ShouldBe([
                ("acrypt", "https://crypter.test/a-a"),
                ("zcrypt", "https://crypter.test/a-z"),
            ]);

        result.UploadSlots[0].LinkCrypters[1].Password.ShouldBe("zpw");

        result
            .UploadSlots[1]
            .LinkCrypters.Select(linkCrypter => linkCrypter.ContainerUrl)
            .ShouldBe(["https://crypter.test/b-z"]);

        result.UploadSlots[2].LinkCrypters.ShouldBeEmpty();
    }

    private ReleaseCollection CreateCollection(string name)
    {
        var collection = new ReleaseCollection
        {
            Name = name,
            Key = name.ToLowerInvariant(),
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            CreatedAt = CreatedAt,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        DbContext.ReleaseCollections.Add(collection);

        return collection;
    }

    private CollectionUploadSlot CreateUploadSlot(
        ReleaseCollection collection,
        string name,
        string key
    )
    {
        var slot = new CollectionUploadSlot
        {
            ReleaseCollection = collection,
            Name = name,
            Key = key,
        };

        DbContext.CollectionUploadSlots.Add(slot);

        return slot;
    }

    private LinkCrypterRegistration CreateLinkCrypterRegistration(string name)
    {
        var registration = new LinkCrypterRegistration
        {
            Name = name,
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };

        DbContext.LinkCrypterRegistrations.Add(registration);

        return registration;
    }

    private void AddContainer(
        CollectionUploadSlot slot,
        LinkCrypterRegistration linkCrypterRegistration,
        string containerUrl,
        string? password,
        LinkCrypterContainerScope scope = LinkCrypterContainerScope.ReleaseCollection
    )
    {
        DbContext.LinkCrypterContainers.Add(
            new LinkCrypterContainer
            {
                Scope = scope,
                CollectionUploadSlot = slot,
                LinkCrypterRegistration = linkCrypterRegistration,
                ContainerUrl = containerUrl,
                Password = password,
                State = LinkCrypterContainerState.Created,
                Errors = [],
                CreatedAt = CreatedAt,
            }
        );
    }
}
