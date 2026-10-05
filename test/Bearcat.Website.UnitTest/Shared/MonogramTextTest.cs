using Bearcat.Website.Shared;
using Shouldly;

namespace Bearcat.Website.UnitTest.Shared;

public class MonogramTextTest
{
    [TestCase("Rapidgator", "Ra")]
    [TestCase("DD", "DD")]
    [TestCase("K", "K")]
    public void GetFromName_ReturnsFirstTwoCharacters(string name, string expectedMonogram)
    {
        // Act
        var monogram = MonogramText.GetFromName(name);

        // Assert
        monogram.ShouldBe(expectedMonogram);
    }
}
