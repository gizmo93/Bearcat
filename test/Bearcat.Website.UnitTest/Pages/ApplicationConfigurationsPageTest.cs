using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Infrastructure.Configuration;
using Bearcat.Website.Pages.ManageApplicationConfigurations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.UnitTest.Pages;

public class ApplicationConfigurationsPageTest
{
    [Test]
    [SetUICulture("en-US")]
    public async Task Render_LoadedConfigurations_ShowsNotificationSettingsWithAnchor()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IJSRuntime, UnusedJsRuntime>();
        services.AddBearcatBlueprintComponents(configuration);
        services.AddApplicationConfiguration<InitialUploadConfiguration>();
        services.AddApplicationConfiguration<NotificationConfiguration>();
        services.AddSingleton<ApplicationConfigurationRegistry>();
        services.AddSingleton<
            IApplicationConfigurationOverrideCache,
            ApplicationConfigurationOverrideCache
        >();
        services.AddSingleton<IApplicationConfigurationOverrideReadRepository, EmptyOverrides>();
        services.AddSingleton<IApplicationConfigurationOverrideWriteRepository, EmptyOverrides>();
        services.AddScoped<ApplicationConfigurationService>();
        services.AddScoped<TimeProvider>();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>()
        );

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<ApplicationConfigurationsPage>(
                ParameterView.Empty
            );

            return component.ToHtmlString();
        });

        html.ShouldContain("<div id=\"notification-settings\">");
        html.ShouldContain("Notification settings");
        html.ShouldContain("Upload completed");
        html.ShouldContain("Availability");
        html.ShouldNotContain("NotificationGroup.");
    }

    private sealed class EmptyOverrides
        : IApplicationConfigurationOverrideReadRepository,
            IApplicationConfigurationOverrideWriteRepository
    {
        public Task<IReadOnlyList<ApplicationConfigurationOverride>> GetAllAsync(
            CancellationToken cancellationToken
        ) => Task.FromResult<IReadOnlyList<ApplicationConfigurationOverride>>([]);

        public Task<ApplicationConfigurationOverride?> GetAsync(
            string configurationKey,
            string propertyName,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public void Add(ApplicationConfigurationOverride configurationOverride) =>
            throw new NotSupportedException();

        public void Remove(ApplicationConfigurationOverride configurationOverride) =>
            throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
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
