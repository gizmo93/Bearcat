using Bearcat.Website.Pages.ManageReleases.Results;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleases.Results;

public class ReleaseSelectionTest
{
    private static readonly IReadOnlyList<int> VisibleReleaseIds = [10, 20, 30, 40, 50];

    [Test]
    public void Toggle_WithoutRange_SelectsOnlyThatRelease()
    {
        // Arrange
        var selection = new ReleaseSelection();

        // Act
        selection.Toggle(VisibleReleaseIds, 20, extendRange: false);

        // Assert
        selection.SelectedReleaseIds.ShouldBe([20]);
    }

    [Test]
    public void Toggle_SelectedReleaseWithoutRange_DeselectsIt()
    {
        // Arrange
        var selection = new ReleaseSelection();
        selection.Toggle(VisibleReleaseIds, 20, extendRange: false);

        // Act
        selection.Toggle(VisibleReleaseIds, 20, extendRange: false);

        // Assert
        selection.Count.ShouldBe(0);
    }

    [Test]
    public void Toggle_WithRangeAfterEarlierToggle_SelectsAllReleasesBetween()
    {
        // Arrange
        var selection = new ReleaseSelection();
        selection.Toggle(VisibleReleaseIds, 20, extendRange: false);

        // Act
        selection.Toggle(VisibleReleaseIds, 40, extendRange: true);

        // Assert
        selection.SelectedReleaseIds.ShouldBe([20, 30, 40], ignoreOrder: true);
    }

    [Test]
    public void Toggle_WithRangeUpwards_SelectsAllReleasesBetween()
    {
        // Arrange
        var selection = new ReleaseSelection();
        selection.Toggle(VisibleReleaseIds, 50, extendRange: false);

        // Act
        selection.Toggle(VisibleReleaseIds, 30, extendRange: true);

        // Assert
        selection.SelectedReleaseIds.ShouldBe([30, 40, 50], ignoreOrder: true);
    }

    [Test]
    public void Toggle_WithRangeOnSelectedRelease_DeselectsAllReleasesBetween()
    {
        // Arrange
        var selection = new ReleaseSelection();
        selection.SelectAll(VisibleReleaseIds);
        selection.Toggle(VisibleReleaseIds, 20, extendRange: false);

        // Act
        selection.Toggle(VisibleReleaseIds, 40, extendRange: true);

        // Assert
        selection.SelectedReleaseIds.ShouldBe([10, 50], ignoreOrder: true);
    }

    [Test]
    public void Toggle_WithRangeWithoutEarlierToggle_SelectsOnlyThatRelease()
    {
        // Arrange
        var selection = new ReleaseSelection();

        // Act
        selection.Toggle(VisibleReleaseIds, 40, extendRange: true);

        // Assert
        selection.SelectedReleaseIds.ShouldBe([40]);
    }

    [Test]
    public void Toggle_WithRangeAfterClear_SelectsOnlyThatRelease()
    {
        // Arrange
        var selection = new ReleaseSelection();
        selection.Toggle(VisibleReleaseIds, 10, extendRange: false);
        selection.Clear();

        // Act
        selection.Toggle(VisibleReleaseIds, 40, extendRange: true);

        // Assert
        selection.SelectedReleaseIds.ShouldBe([40]);
    }

    [Test]
    public void Toggle_WithRangeAfterLastToggledReleaseLeftPage_SelectsOnlyThatRelease()
    {
        // Arrange
        var selection = new ReleaseSelection();
        selection.Toggle(VisibleReleaseIds, 10, extendRange: false);
        selection.KeepOnly([20, 30, 40]);

        // Act
        selection.Toggle([20, 30, 40], 40, extendRange: true);

        // Assert
        selection.SelectedReleaseIds.ShouldBe([40]);
    }

    [Test]
    public void KeepOnly_SelectionWithReleasesNotOnPage_RemovesThem()
    {
        // Arrange
        var selection = new ReleaseSelection();
        selection.SelectAll([10, 20, 30]);

        // Act
        selection.KeepOnly([20, 30, 60]);

        // Assert
        selection.SelectedReleaseIds.ShouldBe([20, 30], ignoreOrder: true);
    }

    [Test]
    public void AreSomeButNotAllSelected_PartialSelection_ReturnsTrue()
    {
        // Arrange
        var selection = new ReleaseSelection();
        selection.Toggle(VisibleReleaseIds, 30, extendRange: false);

        // Act
        var areSomeButNotAllSelected = selection.AreSomeButNotAllSelected(VisibleReleaseIds);

        // Assert
        areSomeButNotAllSelected.ShouldBeTrue();
    }

    [Test]
    public void AreSomeButNotAllSelected_AllSelected_ReturnsFalse()
    {
        // Arrange
        var selection = new ReleaseSelection();
        selection.SelectAll(VisibleReleaseIds);

        // Act
        var areSomeButNotAllSelected = selection.AreSomeButNotAllSelected(VisibleReleaseIds);

        // Assert
        areSomeButNotAllSelected.ShouldBeFalse();
    }

    [Test]
    public void AreAllSelected_NoReleases_ReturnsFalse()
    {
        // Arrange
        var selection = new ReleaseSelection();

        // Act
        var areAllSelected = selection.AreAllSelected([]);

        // Assert
        areAllSelected.ShouldBeFalse();
    }
}
