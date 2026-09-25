using Bearcat.Website.Pages.ManageReleaseTemplates;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleaseTemplates;

public class ArchiveConfigTemplateFormModelTest
{
    [Test]
    public void GetAdditionalArchiveContentIds_SelectionClearedByInput_ReturnsEmptyList()
    {
        // Arrange
        var formModel = new ArchiveConfigTemplateFormModel { AdditionalArchiveContentIds = null };

        // Act
        var result = formModel.GetAdditionalArchiveContentIds();

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public void GetAdditionalArchiveContentIds_SelectedIds_ReturnsIdsInSelectionOrder()
    {
        // Arrange
        var formModel = new ArchiveConfigTemplateFormModel
        {
            AdditionalArchiveContentIds = [7, 3, 12],
        };

        // Act
        var result = formModel.GetAdditionalArchiveContentIds();

        // Assert
        result.ShouldBe([7, 3, 12]);
    }
}
