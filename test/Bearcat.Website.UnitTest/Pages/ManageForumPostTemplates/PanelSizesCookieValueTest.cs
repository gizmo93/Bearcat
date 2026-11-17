using Bearcat.Website.Pages.ManageForumPostTemplates;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageForumPostTemplates;

public class PanelSizesCookieValueTest
{
    [Test]
    [SetCulture("de-DE")]
    public void Format_FractionalSizes_UsesInvariantCultureWithTwoDecimals()
    {
        // Arrange
        double[] sizes = [20.126, 39.874, 40];

        // Act
        var value = PanelSizesCookieValue.Format(sizes);

        // Assert
        value.ShouldBe("20.13,39.87,40");
    }

    [Test]
    [SetCulture("de-DE")]
    public void Parse_ValidValue_ReturnsSizes()
    {
        // Act
        var sizes = PanelSizesCookieValue.Parse("20.5,39.5,40", expectedPanelCount: 3);

        // Assert
        sizes.ShouldBe([20.5, 39.5, 40]);
    }

    [TestCase(null)]
    [TestCase("20,40")]
    [TestCase("-10,60,50")]
    public void Parse_InvalidValue_ReturnsNull(string? value)
    {
        // Act
        var sizes = PanelSizesCookieValue.Parse(value, expectedPanelCount: 3);

        // Assert
        sizes.ShouldBeNull();
    }
}
