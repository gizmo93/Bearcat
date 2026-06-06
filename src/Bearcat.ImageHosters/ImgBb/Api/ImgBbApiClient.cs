using System.Net.Http.Json;
using Bearcat.Abstractions.ImageHoster.Dto;

namespace Bearcat.ImageHosters.ImgBb.Api;

public class ImgBbApiClient(HttpClient httpClient) : IImgBbApiClient
{
    public async Task<UploadResponse> UploadImageAsync(
        string apiKey,
        ImageToUploadDto image,
        CancellationToken cancellationToken = default
    )
    {
        using var content = CreateContent(image);

        using var response = await httpClient.PostAsync(
            CreateUploadUri(apiKey, image.ExpirationSeconds),
            content,
            cancellationToken
        );

        var uploadResponse = await response.Content.ReadFromJsonAsync<UploadResponse>(
            cancellationToken: cancellationToken
        );

        return uploadResponse
            ?? new UploadResponse
            {
                Success = false,
                Status = (int)response.StatusCode,
                Error = new UploadError { Message = response.ReasonPhrase },
            };
    }

    public async Task<UploadResponse> UploadImageAsync(
        string apiKey,
        Stream imageStream,
        string fileName,
        string? name,
        int? expirationSeconds,
        CancellationToken cancellationToken = default
    )
    {
        using var content = CreateContent(imageStream, fileName, name);

        using var response = await httpClient.PostAsync(
            CreateUploadUri(apiKey, expirationSeconds),
            content,
            cancellationToken
        );

        var uploadResponse = await response.Content.ReadFromJsonAsync<UploadResponse>(
            cancellationToken: cancellationToken
        );

        return uploadResponse
            ?? new UploadResponse
            {
                Success = false,
                Status = (int)response.StatusCode,
                Error = new UploadError { Message = response.ReasonPhrase },
            };
    }

    private static Uri CreateUploadUri(string apiKey, int? expirationSeconds)
    {
        var query = $"key={Uri.EscapeDataString(apiKey)}";

        if (expirationSeconds is not null)
        {
            query += $"&expiration={expirationSeconds.Value}";
        }

        return new Uri($"/1/upload?{query}", UriKind.Relative);
    }

    private static HttpContent CreateContent(ImageToUploadDto image)
    {
        if (image.SourceType is ImageUploadSource.LocalFile)
        {
            var stream = File.OpenRead(image.Source);
            var fileName = Path.GetFileName(image.Source);
            return CreateContent(stream, fileName, image.Name);
        }

        var values = new List<KeyValuePair<string, string>> { new("image", image.Source) };

        if (!string.IsNullOrWhiteSpace(image.Name))
        {
            values.Add(new KeyValuePair<string, string>("name", image.Name));
        }

        return new FormUrlEncodedContent(values);
    }

    private static MultipartFormDataContent CreateContent(
        Stream imageStream,
        string fileName,
        string? name
    )
    {
        var content = new MultipartFormDataContent();
        content.Add(new StreamContent(imageStream), "image", fileName);

        if (!string.IsNullOrWhiteSpace(name))
        {
            content.Add(new StringContent(name), "name");
        }

        return content;
    }
}
