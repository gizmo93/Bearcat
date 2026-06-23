using Bearcat.Domain.Shared.QualityGate;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.QualityGate;

public class QualityCheckParameterValuesTest
{
    [Test]
    public void SerializeThenParse_RoundTripsTypedValues()
    {
        // Arrange
        var values = new Dictionary<string, object?>
        {
            ["pattern"] = "*.nfo",
            ["minimumMegabytes"] = 250,
            ["requireNfo"] = true,
        };

        // Act
        var parsed = QualityCheckParameterValues.Parse(
            QualityCheckParameterValues.Serialize(values)
        );

        // Assert
        parsed.GetString("pattern").ShouldBe("*.nfo");
        parsed.GetInt("minimumMegabytes").ShouldBe(250);
        parsed.GetBool("requireNfo").ShouldBeTrue();
    }

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

    [Test]
    public void Read_UsesStoredValue_WhenKeyPresent()
    {
        // Arrange
        var json = QualityCheckParameterValues.Serialize(
            new Dictionary<string, object?> { ["minimumMegabytes"] = 500 }
        );
        var parsed = QualityCheckParameterValues.Parse(json);
        var descriptor = new QualityCheckParameterDescriptor(
            "minimumMegabytes",
            QualityCheckParameterKind.Integer,
            100,
            LabelKey: "MinimumFolderSizeMb"
        );

        // Act
        var value = parsed.Read(descriptor);

        // Assert
        value.ShouldBe(500);
    }
}
