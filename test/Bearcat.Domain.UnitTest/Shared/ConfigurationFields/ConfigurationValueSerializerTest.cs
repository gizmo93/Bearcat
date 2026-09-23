using System.Text.Json;
using Bearcat.Domain.Shared.ConfigurationFields;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ConfigurationFields;

public class ConfigurationValueSerializerTest
{
    [Test]
    public void Serialize_TypedValues_WritesMatchingJsonTypes()
    {
        // Arrange
        var values = new Dictionary<string, object?>
        {
            ["Host"] = "ftp.example.org",
            ["Port"] = 21,
            ["ValidateCertificate"] = false,
        };

        // Act
        var json = ConfigurationValueSerializer.Serialize(values);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("Host").GetString().ShouldBe("ftp.example.org");
        root.GetProperty("Port").ValueKind.ShouldBe(JsonValueKind.Number);
        root.GetProperty("Port").GetInt32().ShouldBe(21);
        root.GetProperty("ValidateCertificate").ValueKind.ShouldBe(JsonValueKind.False);
    }
}
