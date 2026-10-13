using Bearcat.Website.Shared;
using Shouldly;

namespace Bearcat.Website.UnitTest.Shared;

public class SortableListReorderingTest
{
    [TestCase(0, 2, new[] { "B", "C", "A", "D" })]
    [TestCase(3, 0, new[] { "D", "A", "B", "C" })]
    [TestCase(1, 2, new[] { "A", "C", "B", "D" })]
    public void TryMoveItem_ValidMove_MovesItemAndReturnsTrue(
        int oldIndex,
        int newIndex,
        string[] expectedItems
    )
    {
        // Arrange
        var items = new List<string> { "A", "B", "C", "D" };

        // Act
        var result = SortableListReordering.TryMoveItem(items, (oldIndex, newIndex));

        // Assert
        result.ShouldBeTrue();
        items.ShouldBe(expectedItems);
    }

    [TestCase(1, 1)]
    [TestCase(-1, 2)]
    [TestCase(4, 0)]
    [TestCase(0, 4)]
    public void TryMoveItem_SamePositionOrOutOfRange_KeepsOrderAndReturnsFalse(
        int oldIndex,
        int newIndex
    )
    {
        // Arrange
        var items = new List<string> { "A", "B", "C", "D" };

        // Act
        var result = SortableListReordering.TryMoveItem(items, (oldIndex, newIndex));

        // Assert
        result.ShouldBeFalse();
        items.ShouldBe(["A", "B", "C", "D"]);
    }
}
