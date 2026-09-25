using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageAdditionalArchiveContents;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageAdditionalArchiveContents;

public class AdditionalArchiveContentFormModelTest
{
    [Test]
    public void ToInput_FieldsClearedByInputs_MapsNameToEmptyAndKeepsOtherFieldsNull()
    {
        // Arrange
        var formModel = new AdditionalArchiveContentFormModel
        {
            Name = null,
            Type = AdditionalArchiveContentType.TextFile,
            SourcePath = null,
            FileName = null,
            TextContent = null,
        };

        // Act
        var result = formModel.ToInput();

        // Assert
        result.ShouldBe(
            new AdditionalArchiveContentInput(
                string.Empty,
                AdditionalArchiveContentType.TextFile,
                SourcePath: null,
                FileName: null,
                TextContent: null
            )
        );
    }

    [Test]
    public void ToInput_FilledFields_MapsValuesUnchanged()
    {
        // Arrange
        var formModel = new AdditionalArchiveContentFormModel
        {
            Name = " Premium ad ",
            Type = AdditionalArchiveContentType.TextFile,
            SourcePath = "/data/ads",
            FileName = "premium.txt",
            TextContent = "  Buy premium.\n",
        };

        // Act
        var result = formModel.ToInput();

        // Assert
        result.ShouldBe(
            new AdditionalArchiveContentInput(
                " Premium ad ",
                AdditionalArchiveContentType.TextFile,
                "/data/ads",
                "premium.txt",
                "  Buy premium.\n"
            )
        );
    }
}
