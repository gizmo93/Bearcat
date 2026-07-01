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
    public void FileInfoResult_NumericDownloadsField_DeserializesDownloadsAsString()
    {
        // Arrange
        const string json = """
            {
              "msg": "OK",
              "status": 200,
              "result": [
                {
                  "filecode": "abc123xyz456",
                  "status": 200,
                  "name": "video.mp4",
                  "downloads": 150
                }
              ]
            }
            """;

        // Act
        var response = JsonSerializer.Deserialize<FileInfoResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        // Assert
        response.ShouldNotBeNull();
        response.Results.ShouldHaveSingleItem();
        response.Results[0].Downloads.ShouldBe("150");
    }

    [Test]
    public void FileInfoResult_StringDownloadSingularField_DeserializesDownloadAsString()
    {
        // Arrange
        const string json = """
            {
              "msg": "OK",
              "status": 200,
              "result": [
                {
                  "filecode": "gi4o0tlro01u",
                  "status": 200,
                  "name": "clip.mp4",
                  "download": "0"
                }
              ]
            }
            """;

        // Act
        var response = JsonSerializer.Deserialize<FileInfoResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        // Assert
        response.ShouldNotBeNull();
        response.Results.ShouldHaveSingleItem();
        response.Results[0].Download.ShouldBe("0");
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
