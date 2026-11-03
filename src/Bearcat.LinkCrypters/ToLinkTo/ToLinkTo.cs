using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.Extensions;
using Bearcat.LinkCrypters.ToLinkTo.Api;
using Bearcat.LinkCrypters.ToLinkTo.Api.CreateFolder;
using Refit;

namespace Bearcat.LinkCrypters.ToLinkTo;

public class ToLinkTo(IToLinkToApi api) : ILinkCrypterWithContainerReading
{
    private const int ListFoldersPageSize = 200;

    public string Name => "tolink.to";

    public List<string> ConfigurationKeys => [nameof(ToLinkToConfig.ApiKey)];

    public bool SupportsCaptcha => true;

    public bool SupportsContainerDownload => true;

    public bool SupportsClickAndLoad => true;

    public async Task<CreateContainerResult> CreateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerName,
        string? password,
        IReadOnlyDictionary<int, IReadOnlyList<string>> linksPerMirrorNumber,
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
                    Links = CreateLinksValue(linksPerMirrorNumber),
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
        IReadOnlyDictionary<int, IReadOnlyList<string>> linksPerMirrorNumber,
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
                    Links = CreateLinksValue(linksPerMirrorNumber),
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

    public async Task<ListContainersResult> ListContainersAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = linkCrypterConfig.As<ToLinkToConfig>();
        var containers = new List<ListedContainer>();

        try
        {
            var page = 1;
            int pageCount;

            do
            {
                var response = await api.ListFoldersAsync(
                    order: "ASC",
                    limit: ListFoldersPageSize,
                    page: page,
                    apiKey: config.ApiKey,
                    cancellationToken: cancellationToken
                );

                containers.AddRange(
                    response.Folders.Select(folder => new ListedContainer(
                        ContainerId: folder.Folder,
                        ContainerUrl: $"https://tolink.to/f/{folder.Folder}",
                        ContainerName: folder.Title,
                        ExternalReference: folder.Folder
                    ))
                );

                pageCount = response.Pages;
                page++;
            } while (page <= pageCount);

            return new ListContainersResult(
                IsSuccess: true,
                Containers: containers,
                ErrorMessages: []
            );
        }
        catch (Exception ex)
        {
            return new ListContainersResult(
                IsSuccess: false,
                Containers: [],
                ErrorMessages: [await GetErrorMessageAsync(ex)]
            );
        }
    }

    public async Task<ReadContainerLinksResult> ReadContainerLinksAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerUrl,
        string? externalReference,
        CancellationToken cancellationToken
    )
    {
        var config = linkCrypterConfig.As<ToLinkToConfig>();
        var folder = !string.IsNullOrWhiteSpace(externalReference)
            ? externalReference
            : GetFolderAlias(containerUrl);

        try
        {
            var response = await api.GetFolderDetailsAsync(
                folder: folder,
                apiKey: config.ApiKey,
                cancellationToken: cancellationToken
            );

            return new ReadContainerLinksResult(
                IsSuccess: true,
                LinksPerMirrorNumber: GroupLinksPerHostAsMirrors(
                    response.Links.Select(link => link.Url).ToList()
                ),
                HasBackupLinks: false,
                ErrorMessages: []
            );
        }
        catch (Exception ex)
        {
            return new ReadContainerLinksResult(
                IsSuccess: false,
                LinksPerMirrorNumber: new Dictionary<int, IReadOnlyList<string>>(),
                HasBackupLinks: false,
                ErrorMessages: [await GetErrorMessageAsync(ex)]
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

    private static string CreateLinksValue(
        IReadOnlyDictionary<int, IReadOnlyList<string>> linksPerMirrorNumber
    )
    {
        return string.Join(
            ';',
            linksPerMirrorNumber.OrderBy(mirror => mirror.Key).SelectMany(mirror => mirror.Value)
        );
    }

    private static Dictionary<int, IReadOnlyList<string>> GroupLinksPerHostAsMirrors(
        IReadOnlyList<string> links
    )
    {
        return links
            .GroupBy(GetHostWithoutWww, StringComparer.OrdinalIgnoreCase)
            .Select((linksOfHost, index) => (MirrorNumber: index + 1, Links: linksOfHost.ToList()))
            .ToDictionary(
                mirror => mirror.MirrorNumber,
                IReadOnlyList<string> (mirror) => mirror.Links
            );
    }

    private static string GetHostWithoutWww(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return link;
        }

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
    }

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
