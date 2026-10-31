using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.Extensions;
using Bearcat.LinkCrypters.ToLinkTo.Api;
using Bearcat.LinkCrypters.ToLinkTo.Api.CreateFolder;
using Refit;

namespace Bearcat.LinkCrypters.ToLinkTo;

public class ToLinkTo(IToLinkToApi api) : ILinkCrypter
{
    public string Name => "tolink.to";

    public List<string> ConfigurationKeys => [nameof(ToLinkToConfig.ApiKey)];

    public bool SupportsCaptcha => true;

    public bool SupportsContainerDownload => true;

    public bool SupportsClickAndLoad => true;

    public async Task<CreateContainerResult> CreateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerName,
        string? password,
        IReadOnlyList<string> links,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<ToLinkToConfig>();

        try
        {
            var response = await api.CreateFolderAsync(
                request: new RequestBody
                {
                    Title = containerName,
                    Links = CreateLinksValue(links),
                    Options = CreateFolderOptions(
                        password,
                        enableCaptcha,
                        enableContainerDownload,
                        enableClickAndLoad
                    ),
                },
                apiKey: config.ApiKey,
                cancellationToken: cancellationToken
            );

            return new CreateContainerResult(
                IsSuccess: true,
                ContainerLink: response.Url,
                ExternalReference: response.Folder,
                ErrorMessages: []
            );
        }
        catch (Exception ex)
        {
            return new CreateContainerResult(
                IsSuccess: false,
                ContainerLink: null,
                ExternalReference: null,
                ErrorMessages: [await GetErrorMessageAsync(ex)]
            );
        }
    }

    public async Task<UpdateContainerResult> UpdateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerLink,
        string? externalReference,
        string? password,
        IReadOnlyList<string> links,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<ToLinkToConfig>();

        try
        {
            var folder = !string.IsNullOrWhiteSpace(externalReference)
                ? externalReference
                : GetFolderAlias(containerLink);

            await api.EditFolderAsync(
                folder: folder,
                request: new Api.EditFolder.RequestBody
                {
                    Links = CreateLinksValue(links),
                    Options = CreateFolderOptions(
                        password: password,
                        enableCaptcha: enableCaptcha,
                        enableContainerDownload: enableContainerDownload,
                        enableClickAndLoad: enableClickAndLoad
                    ),
                },
                apiKey: config.ApiKey,
                cancellationToken: cancellationToken
            );

            return new UpdateContainerResult(
                Outcome: UpdateContainerOutcome.Success,
                ErrorMessage: null
            );
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return new UpdateContainerResult(
                Outcome: UpdateContainerOutcome.ContainerNotFound,
                ErrorMessage: await GetErrorMessageAsync(ex)
            );
        }
        catch (Exception ex)
        {
            return new UpdateContainerResult(
                Outcome: UpdateContainerOutcome.Failed,
                ErrorMessage: await GetErrorMessageAsync(ex)
            );
        }
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        return JsonSerializer.Serialize(config);
    }

    public ILinkCrypterConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<ToLinkToConfig>(serializedConfig)!;
    }

    public async Task<TryLoginResult> TryLoginAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<ToLinkToConfig>();

        try
        {
            await api.PingAsync(config.ApiKey, cancellationToken);

            return new TryLoginResult(IsSuccess: true, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: await GetErrorMessageAsync(ex)
            );
        }
    }

    private static FolderOptions CreateFolderOptions(
        string? password,
        bool enableCaptcha,
        bool enableContainerDownload,
        bool enableClickAndLoad
    )
    {
        return new FolderOptions
        {
            Web = true,
            Container = enableContainerDownload,
            ClickAndLoad = enableClickAndLoad,
            Captcha = enableCaptcha,
            CaptchaText = false,
            Password = password ?? string.Empty,
        };
    }

    private static string CreateLinksValue(IReadOnlyList<string> links) => string.Join(';', links);

    private static async Task<string> GetErrorMessageAsync(Exception exception)
    {
        if (exception is not ApiException apiException)
        {
            return exception.GetBaseException().Message;
        }

        var errorMessage = await ReadErrorMessageAsync(apiException);

        return !string.IsNullOrWhiteSpace(errorMessage)
            ? $"ToLink.to returned {(int)apiException.StatusCode}: {errorMessage}"
            : apiException.Message;
    }

    private static async Task<string?> ReadErrorMessageAsync(ApiException apiException)
    {
        try
        {
            var errorResponse = await apiException.GetContentAsAsync<ErrorResponseBody>();
            return errorResponse?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetFolderAlias(string folderLinkOrAlias)
    {
        return Uri.TryCreate(folderLinkOrAlias, UriKind.Absolute, out var uri)
            ? uri.Segments.Last().Trim('/')
            : folderLinkOrAlias.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
    }
}
