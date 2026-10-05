using Bearcat.Website.Pages.ManageReleases.Results;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleases.Results;

public class ReleaseKeyboardFocusTest
{
    private static readonly IReadOnlyList<int> VisibleReleaseIds = [10, 20, 30];

    [Test]
    public void MoveToNext_WithoutFocusedRelease_FocusesFirstRelease()
    {
        // Arrange
        var keyboardFocus = new ReleaseKeyboardFocus();

        // Act
        keyboardFocus.MoveToNext(VisibleReleaseIds);

        // Assert
        keyboardFocus.FocusedReleaseId.ShouldBe(10);
    }

    [Test]
    public void MoveToPrevious_WithoutFocusedRelease_FocusesFirstRelease()
    {
        // Arrange
        var keyboardFocus = new ReleaseKeyboardFocus();

        // Act
        keyboardFocus.MoveToPrevious(VisibleReleaseIds);

        // Assert
        keyboardFocus.FocusedReleaseId.ShouldBe(10);
    }

    [Test]
    public void MoveToNext_WithFocusedRelease_FocusesFollowingRelease()
    {
        // Arrange
        var keyboardFocus = new ReleaseKeyboardFocus();
        keyboardFocus.Focus(20);

        // Act
        keyboardFocus.MoveToNext(VisibleReleaseIds);

        // Assert
        keyboardFocus.FocusedReleaseId.ShouldBe(30);
    }

    [Test]
    public void MoveToNext_OnLastRelease_StaysOnLastRelease()
    {
        // Arrange
        var keyboardFocus = new ReleaseKeyboardFocus();
        keyboardFocus.Focus(30);

        // Act
        keyboardFocus.MoveToNext(VisibleReleaseIds);

        // Assert
        keyboardFocus.FocusedReleaseId.ShouldBe(30);
    }

    [Test]
    public void MoveToPrevious_OnFirstRelease_StaysOnFirstRelease()
    {
        // Arrange
        var keyboardFocus = new ReleaseKeyboardFocus();
        keyboardFocus.Focus(10);

        // Act
        keyboardFocus.MoveToPrevious(VisibleReleaseIds);

        // Assert
        keyboardFocus.FocusedReleaseId.ShouldBe(10);
    }

    [Test]
    public void MoveToPrevious_WithFocusedRelease_FocusesPrecedingRelease()
    {
        // Arrange
        var keyboardFocus = new ReleaseKeyboardFocus();
        keyboardFocus.Focus(30);

        // Act
        keyboardFocus.MoveToPrevious(VisibleReleaseIds);

        // Assert
        keyboardFocus.FocusedReleaseId.ShouldBe(20);
    }

    [Test]
    public void MoveToNext_WithFocusedReleaseNoLongerVisible_FocusesFirstRelease()
    {
        // Arrange
        var keyboardFocus = new ReleaseKeyboardFocus();
        keyboardFocus.Focus(99);

        // Act
        keyboardFocus.MoveToNext(VisibleReleaseIds);

        // Assert
        keyboardFocus.FocusedReleaseId.ShouldBe(10);
    }

    [Test]
    public void MoveToNext_WithoutVisibleReleases_KeepsNoFocus()
    {
        // Arrange
        var keyboardFocus = new ReleaseKeyboardFocus();

        // Act
        keyboardFocus.MoveToNext([]);

        // Assert
        keyboardFocus.FocusedReleaseId.ShouldBeNull();
    }

    [Test]
    public void KeepOnly_WithFocusedReleaseNotVisible_ClearsFocus()
    {
        // Arrange
        var keyboardFocus = new ReleaseKeyboardFocus();
        keyboardFocus.Focus(20);

        // Act
        keyboardFocus.KeepOnly([10, 30]);

        // Assert
        keyboardFocus.FocusedReleaseId.ShouldBeNull();
    }

    [Test]
    public void KeepOnly_WithFocusedReleaseVisible_KeepsFocus()
    {
        // Arrange
        var keyboardFocus = new ReleaseKeyboardFocus();
        keyboardFocus.Focus(20);

        // Act
        keyboardFocus.KeepOnly(VisibleReleaseIds);

        // Assert
        keyboardFocus.FocusedReleaseId.ShouldBe(20);
    }
}
