using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Website.Pages.ManageDistributionSites;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages;

public class DistributionSiteDialogTest
{
    [Test]
    [SetUICulture("en-US")]
    public async Task Render_UnknownPlugin_UsesFieldMetadataAndStoredConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IJSRuntime, UnusedJsRuntime>();
        services.AddBearcatBlueprintComponents(configuration);
        services.AddSingleton<IDistributionSiteFactory, TestSiteFactory>();
        services.AddSingleton<
            IDistributionSiteRegistrationReadRepository,
            TestRegistrationRepository
        >();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>()
        );
        var parameters = ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(CreateOrEditDialog.DistributionSiteRegistrationId)] = 42,
            }
        );

        // Act
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<CreateOrEditDialog>(parameters);
            return component.ToHtmlString();
        });

        // Assert
        html.ShouldContain("Forum URL");
        html.ShouldContain("placeholder=\"https://custom.example/forum/\"");
        html.ShouldContain("value=\"https://saved.example/community/\"");
        html.ShouldContain("type=\"password\"");
        html.ShouldContain("AccessCode");
        html.ShouldContain("Compatibility is not guaranteed.");
        html.ShouldNotContain("GenericXenForoHelp");
    }

    private sealed class TestSiteFactory : IDistributionSiteFactory
    {
        public IReadOnlyList<DistributionSiteDto> GetDistributionSites() =>
            [
                new(
                    Name: "Custom forum",
                    ClassName: "CustomForum",
                    Kind: DistributionSiteKind.Forum,
                    ConfigurationFields:
                    [
                        new(
                            Key: "Endpoint",
                            LabelResourceKey: "ForumBaseUrl",
                            Placeholder: "https://custom.example/forum/",
                            PrefillOnEdit: true
                        ),
                        new(Key: "AccessCode", IsSecret: true),
                    ],
                    ConfigurationHelpResourceKey: "GenericXenForoHelp"
                ),
            ];

        public IDistributionSite Get(string className) => throw new NotSupportedException();

        public IDistributionSite Create(string className, string serializedConfig) =>
            throw new NotSupportedException();
    }

    private sealed class TestRegistrationRepository : IDistributionSiteRegistrationReadRepository
    {
        public Task<IReadOnlyList<DistributionSiteRegistrationReadModel>> GetAllAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<DistributionSiteRegistrationReadModel?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default
        )
        {
            var registration = new DistributionSiteRegistrationReadModel(
                DistributionSiteRegistrationId: id,
                Name: "My forum",
                DistributionSiteClassName: "CustomForum",
                DistributionSiteName: "Custom forum",
                Kind: DistributionSiteKind.Forum,
                IsActive: true,
                EnableAutomaticPosting: false,
                StripDotsForThreadSearch: true,
                PostingRuleCount: 0,
                BaseUrl: "https://saved.example/community/",
                Configuration: new Dictionary<string, string>
                {
                    ["Endpoint"] = "https://saved.example/community/",
                }
            );

            return Task.FromResult<DistributionSiteRegistrationReadModel?>(registration);
        }
    }

    private sealed class UnusedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args
        ) => throw new NotSupportedException();
    }
}
