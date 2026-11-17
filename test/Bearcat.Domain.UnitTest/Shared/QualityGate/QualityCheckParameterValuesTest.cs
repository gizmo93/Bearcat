using Bearcat.Domain.Shared.QualityGate;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.QualityGate;

public class QualityCheckParameterValuesTest
{
    [Test]
    public void Getters_ReturnFallback_WhenKeyMissing()
    {
        // Arrange
        var parsed = QualityCheckParameterValues.Parse("{}");

        // Act + Assert
        parsed.GetString("pattern", "*.sfv").ShouldBe("*.sfv");
        parsed.GetInt("minimumMegabytes", 100).ShouldBe(100);
        parsed.GetBool("requireNfo", true).ShouldBeTrue();
    }

    [Test]
    public void Read_UsesDescriptorDefault_WhenKeyMissing()
    {
        // Arrange
        var parsed = QualityCheckParameterValues.Parse("{}");
        var textDescriptor = new QualityCheckParameterDescriptor(
            "pattern",
            QualityCheckParameterKind.Text,
            "*.nfo",
            LabelKey: "FilePattern"
        );
        var integerDescriptor = new QualityCheckParameterDescriptor(
            "minimumMegabytes",
            QualityCheckParameterKind.Integer,
            100,
            LabelKey: "MinimumFolderSizeMb"
        );
        var booleanDescriptor = new QualityCheckParameterDescriptor(
            "requireNfo",
            QualityCheckParameterKind.Boolean,
            true,
            LabelKey: "RequireNfo"
        );

        // Act + Assert
        parsed.Read(textDescriptor).ShouldBe("*.nfo");
        parsed.Read(integerDescriptor).ShouldBe(100);
        parsed.Read(booleanDescriptor).ShouldBe(true);
    }
}
