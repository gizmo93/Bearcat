using Bearcat.Domain.Shared.ForumPosting;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ForumPosting;

public class ForumServiceRestrictionFilterTest
{
    [Test]
    public void RemoveForbiddenItemsAndFindBlockedReason_ForbiddenItem_RemovesItWithoutBlocking()
    {
        // Act
        var result = ForumServiceRestrictionFilter.RemoveForbiddenItemsAndFindBlockedReason(
            items: ["Rapidgator", "DDownload", "Katfile"],
            getClassName: className => className,
            requiredClassNames: [],
            forbiddenClassNames: ["Katfile"],
            kindName: "file hoster"
        );

        // Assert
        result.AllowedItems.ShouldBe(["Rapidgator", "DDownload"]);
        result.BlockedReason.ShouldBeNull();
    }

    [Test]
    public void RemoveForbiddenItemsAndFindBlockedReason_AllItemsForbidden_BlocksWithNothingLeftReason()
    {
        // Act
        var result = ForumServiceRestrictionFilter.RemoveForbiddenItemsAndFindBlockedReason(
            items: ["Katfile", "Katfile"],
            getClassName: className => className,
            requiredClassNames: [],
            forbiddenClassNames: ["Katfile"],
            kindName: "file hoster"
        );

        // Assert
        result.AllowedItems.ShouldBeEmpty();
        result.BlockedReason.ShouldBe("No allowed file hoster is left.");
    }

    [Test]
    public void RemoveForbiddenItemsAndFindBlockedReason_NoItemsBefore_DoesNotBlock()
    {
        // Act
        var result = ForumServiceRestrictionFilter.RemoveForbiddenItemsAndFindBlockedReason(
            items: Array.Empty<string>(),
            getClassName: className => className,
            requiredClassNames: [],
            forbiddenClassNames: ["Katfile"],
            kindName: "image hoster"
        );

        // Assert
        result.AllowedItems.ShouldBeEmpty();
        result.BlockedReason.ShouldBeNull();
    }

    [Test]
    public void RemoveForbiddenItemsAndFindBlockedReason_OneRequiredItemPresent_DoesNotBlock()
    {
        // Act
        var result = ForumServiceRestrictionFilter.RemoveForbiddenItemsAndFindBlockedReason(
            items: ["DDownload", "Katfile"],
            getClassName: className => className,
            requiredClassNames: ["Rapidgator", "DDownload"],
            forbiddenClassNames: [],
            kindName: "file hoster"
        );

        // Assert
        result.AllowedItems.ShouldBe(["DDownload", "Katfile"]);
        result.BlockedReason.ShouldBeNull();
    }

    [Test]
    public void RemoveForbiddenItemsAndFindBlockedReason_NoRequiredItemPresent_BlocksWithRequiredReason()
    {
        // Act
        var result = ForumServiceRestrictionFilter.RemoveForbiddenItemsAndFindBlockedReason(
            items: ["Katfile"],
            getClassName: className => className,
            requiredClassNames: ["Rapidgator", "DDownload"],
            forbiddenClassNames: [],
            kindName: "file hoster"
        );

        // Assert
        result.AllowedItems.ShouldBe(["Katfile"]);
        result.BlockedReason.ShouldBe(
            "None of the required file hosters is present: Rapidgator, DDownload."
        );
    }

    [Test]
    public void RemoveForbiddenItemsAndFindBlockedReason_RequiredItemOnlyForbidden_BlocksWithRequiredReason()
    {
        // Act
        var result = ForumServiceRestrictionFilter.RemoveForbiddenItemsAndFindBlockedReason(
            items: ["Rapidgator", "Katfile"],
            getClassName: className => className,
            requiredClassNames: ["Rapidgator"],
            forbiddenClassNames: ["Rapidgator"],
            kindName: "file hoster"
        );

        // Assert
        result.AllowedItems.ShouldBe(["Katfile"]);
        result.BlockedReason.ShouldBe("None of the required file hosters is present: Rapidgator.");
    }
}
