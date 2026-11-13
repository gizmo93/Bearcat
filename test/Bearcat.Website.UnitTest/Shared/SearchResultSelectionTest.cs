using Bearcat.Website.Shared;
using Shouldly;

namespace Bearcat.Website.UnitTest.Shared;

public class SearchResultSelectionTest
{
    private static readonly IReadOnlyList<int> VisibleIds = [10, 20, 30, 40, 50];

    [Test]
    public void Toggle_WithoutRange_SelectsOnlyThatItem()
    {
        // Arrange
        var selection = new SearchResultSelection();

        // Act
        selection.Toggle(VisibleIds, 20, extendRange: false);

        // Assert
        selection.SelectedIds.ShouldBe([20]);
    }

    [Test]
    public void Toggle_SelectedItemWithoutRange_DeselectsIt()
    {
        // Arrange
        var selection = new SearchResultSelection();
        selection.Toggle(VisibleIds, 20, extendRange: false);

        // Act
        selection.Toggle(VisibleIds, 20, extendRange: false);

        // Assert
        selection.Count.ShouldBe(0);
    }

    [Test]
    public void Toggle_WithRangeAfterEarlierToggle_SelectsAllItemsBetween()
    {
        // Arrange
        var selection = new SearchResultSelection();
        selection.Toggle(VisibleIds, 20, extendRange: false);

        // Act
        selection.Toggle(VisibleIds, 40, extendRange: true);

        // Assert
        selection.SelectedIds.ShouldBe([20, 30, 40], ignoreOrder: true);
    }

    [Test]
    public void Toggle_WithRangeUpwards_SelectsAllItemsBetween()
    {
        // Arrange
        var selection = new SearchResultSelection();
        selection.Toggle(VisibleIds, 50, extendRange: false);

        // Act
        selection.Toggle(VisibleIds, 30, extendRange: true);

        // Assert
        selection.SelectedIds.ShouldBe([30, 40, 50], ignoreOrder: true);
    }

    [Test]
    public void Toggle_WithRangeOnSelectedItem_DeselectsAllItemsBetween()
    {
        // Arrange
        var selection = new SearchResultSelection();
        selection.SelectAll(VisibleIds);
        selection.Toggle(VisibleIds, 20, extendRange: false);

        // Act
        selection.Toggle(VisibleIds, 40, extendRange: true);

        // Assert
        selection.SelectedIds.ShouldBe([10, 50], ignoreOrder: true);
    }

    [Test]
    public void Toggle_WithRangeWithoutEarlierToggle_SelectsOnlyThatItem()
    {
        // Arrange
        var selection = new SearchResultSelection();

        // Act
        selection.Toggle(VisibleIds, 40, extendRange: true);

        // Assert
        selection.SelectedIds.ShouldBe([40]);
    }

    [Test]
    public void Toggle_WithRangeAfterClear_SelectsOnlyThatItem()
    {
        // Arrange
        var selection = new SearchResultSelection();
        selection.Toggle(VisibleIds, 10, extendRange: false);
        selection.Clear();

        // Act
        selection.Toggle(VisibleIds, 40, extendRange: true);

        // Assert
        selection.SelectedIds.ShouldBe([40]);
    }

    [Test]
    public void Toggle_WithRangeAfterLastToggledItemLeftPage_SelectsOnlyThatItem()
    {
        // Arrange
        var selection = new SearchResultSelection();
        selection.Toggle(VisibleIds, 10, extendRange: false);
        selection.KeepOnly([20, 30, 40]);

        // Act
        selection.Toggle([20, 30, 40], 40, extendRange: true);

        // Assert
        selection.SelectedIds.ShouldBe([40]);
    }

    [Test]
    public void KeepOnly_SelectionWithItemsNotOnPage_RemovesThem()
    {
        // Arrange
        var selection = new SearchResultSelection();
        selection.SelectAll([10, 20, 30]);

        // Act
        selection.KeepOnly([20, 30, 60]);

        // Assert
        selection.SelectedIds.ShouldBe([20, 30], ignoreOrder: true);
    }

    [Test]
    public void AreSomeButNotAllSelected_PartialSelection_ReturnsTrue()
    {
        // Arrange
        var selection = new SearchResultSelection();
        selection.Toggle(VisibleIds, 30, extendRange: false);

        // Act
        var areSomeButNotAllSelected = selection.AreSomeButNotAllSelected(VisibleIds);

        // Assert
        areSomeButNotAllSelected.ShouldBeTrue();
    }

    [Test]
    public void AreSomeButNotAllSelected_AllSelected_ReturnsFalse()
    {
        // Arrange
        var selection = new SearchResultSelection();
        selection.SelectAll(VisibleIds);

        // Act
        var areSomeButNotAllSelected = selection.AreSomeButNotAllSelected(VisibleIds);

        // Assert
        areSomeButNotAllSelected.ShouldBeFalse();
    }

    [Test]
    public void AreAllSelected_NoItems_ReturnsFalse()
    {
        // Arrange
        var selection = new SearchResultSelection();

        // Act
        var areAllSelected = selection.AreAllSelected([]);

        // Assert
        areAllSelected.ShouldBeFalse();
    }
}
