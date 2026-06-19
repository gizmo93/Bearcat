using System.Text.Json;
using Bearcat.Hosters.Shared.XFilesharing.Api;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.ClicknUpload;

public class ApiClientTest
{
    [Test]
    public void RequestUploadResponse_UploadUrlField_MapsUploadUrl()
    {
        // Arrange
        const string json = """
            {
              "server_time": "2026-06-19 23:27:06",
              "sess_id": "lf1kcgd0uoxt8nml",
              "status": 200,
              "upload_url": "https://pink01.clicknupload.net/cgi-bin/upload.cgi",
              "msg": "OK"
            }
            """;

        // Act
        var response = JsonSerializer.Deserialize<RequestUploadResponse>(json);

        // Assert
        response.ShouldNotBeNull();
        response.SessionId.ShouldBe("lf1kcgd0uoxt8nml");
        response.UploadUrl.ShouldBe("https://pink01.clicknupload.net/cgi-bin/upload.cgi");
    }

    [Test]
    public void RequestUploadResponse_ResultField_MapsUploadUrl()
    {
        // Arrange
        const string json = """
            {
              "sess_id": "session-id",
              "status": 200,
              "result": "https://server.example/upload.cgi",
              "msg": "OK"
            }
            """;

        // Act
        var response = JsonSerializer.Deserialize<RequestUploadResponse>(json);

        // Assert
        response.ShouldNotBeNull();
        response.UploadUrl.ShouldBe("https://server.example/upload.cgi");
    }
}
