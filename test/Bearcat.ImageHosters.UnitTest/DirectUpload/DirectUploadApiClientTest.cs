using System.Net;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.ImageHosters.DirectUpload.Api;
using Shouldly;

namespace Bearcat.ImageHosters.UnitTest.DirectUpload;

public class DirectUploadApiClientTest
{
    private const string UploadToken = "S#1.92037806";
    private const string DirectUrl = "https://s1.directupload.eu/images/260620/sf2opb5w.png";
    private const string ThumbnailUrl =
        "https://s1.directupload.eu/images/260620/temp/sf2opb5w.png";
    private const string DeleteUrl = "https://www.directupload.eu/delfile/Ly9XcW15djE3RVE9/";

    private static readonly string ResultPage = $$"""
        <h1>Dein Bild wurde erfolgreich hochgeladen!</h1>
        <script>
            Linkliste[0][1] = 'https://www.directupload.eu/file/d/9321/sf2opb5w_png.htm';
            Linkliste[0][2] = '[URL=https://www.directupload.eu/file/d/9321/sf2opb5w_png.htm][IMG]{{ThumbnailUrl}}[/IMG][/URL]';
            Linkliste[0][6] = '{{DirectUrl}}';
            Linkliste[0][7] = '{{DeleteUrl}}';
        </script>
        """;

    private RecordingHandler handler = null!;
    private HttpClient httpClient = null!;
    private DirectUploadApiClient client = null!;

    [SetUp]
    public void SetUp()
    {
        handler = new RecordingHandler();
        httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(DirectUploadApiClient.BaseUrl),
        };
        client = new DirectUploadApiClient(httpClient);
    }

    [TearDown]
    public void TearDown()
    {
        httpClient.Dispose();
        handler.Dispose();
    }

    [Test]
    public async Task UploadImageAsync_ParsesLinksFromResultPage()
    {
        // Arrange
        var image = new ImageToUploadDto(
            Source: Convert.ToBase64String([1, 2, 3, 4]),
            SourceType: ImageUploadSource.Base64,
            Name: "cover.png"
        );

        // Act
        var response = await client.UploadImageAsync(image, CancellationToken.None);

        // Assert
        response.ImageId.ShouldBe("92037806");
        response.DirectUrl.ShouldBe(DirectUrl);
        response.ThumbnailUrl.ShouldBe(ThumbnailUrl);
        response.DeleteUrl.ShouldBe(DeleteUrl);
    }

    [Test]
    public async Task UploadImageAsync_SendsImageAsDataUrlAndThreadsSessionCookie()
    {
        // Arrange
        var image = new ImageToUploadDto(
            Convert.ToBase64String([1, 2, 3, 4]),
            ImageUploadSource.Base64,
            "cover.png"
        );

        // Act
        await client.UploadImageAsync(image, CancellationToken.None);

        // Assert
        handler.UploadBody.ShouldContain("data:image/png;base64,AQIDBA==");
        handler.UploadBody.ShouldContain("name=file");
        handler.UploadBody.ShouldContain("name=filename");
        handler.UploadBody.ShouldContain("cover.png");
        handler.SubmitBody.ShouldContain("img_id%5B%5D=S%231.92037806");
        handler.SubmitCookie.ShouldBe("PHPSESSID=testsession");
    }

    [Test]
    public async Task UploadImageAsync_UrlSourceWithExtensionlessName_AppendsImageExtension()
    {
        // Arrange - a display name like "Some.Show.2023" has a dot but no recognised image
        // extension; directupload rejects such filenames, so it must be forced to ".jpg".
        var image = new ImageToUploadDto(
            Source: "https://artworks.example/series/417257/posters/653543d2b6ccb",
            SourceType: ImageUploadSource.Url,
            Name: "Some.Show.2023"
        );

        // Act
        await client.UploadImageAsync(image, CancellationToken.None);

        // Assert
        handler.UploadBody.ShouldContain("Some.Show.2023.jpg");
        handler.UploadBody.ShouldContain("data:image/jpeg;base64,");
    }

    [Test]
    public void UploadImageAsync_UploadRejected_Throws()
    {
        // Arrange
        handler.UploadResponseBody = "E#error";
        var image = new ImageToUploadDto(
            Convert.ToBase64String([1, 2, 3, 4]),
            ImageUploadSource.Base64,
            "cover.png"
        );

        // Act / Assert
        Should.ThrowAsync<DirectUploadApiException>(() =>
            client.UploadImageAsync(image, CancellationToken.None)
        );
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string UploadResponseBody { get; set; } = UploadToken;
        public string UploadBody { get; private set; } = "";
        public string SubmitBody { get; private set; } = "";
        public string? SubmitCookie { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (request.Method == HttpMethod.Get)
            {
                var imageContent = new ByteArrayContent([1, 2, 3, 4]);
                imageContent.Headers.TryAddWithoutValidation("Content-Type", "image/jpeg");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = imageContent };
            }

            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (path.EndsWith("upload_http_resize.php", StringComparison.Ordinal))
            {
                UploadBody = body;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(UploadResponseBody),
                };
                response.Headers.TryAddWithoutValidation(
                    "Set-Cookie",
                    "PHPSESSID=testsession; path=/"
                );
                return response;
            }

            SubmitBody = body;
            SubmitCookie = request.Headers.TryGetValues("Cookie", out var cookies)
                ? string.Join("; ", cookies)
                : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResultPage),
            };
        }
    }
}
