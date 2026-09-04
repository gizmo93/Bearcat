using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.Dto;
using Bearcat.Domain.UseCases.ManageNotifications.ReadModels;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageNotifications;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages;

public class NotificationsPageTest
{
    [Test]
    [SetUICulture("en-US")]
    public async Task Render_LegacyNotification_ShowsSeverityInTableAndKindFilterAboveIt()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBearcatBlueprintComponents(new ConfigurationBuilder().Build());
        services.AddSingleton<IJSRuntime, UnusedJsRuntime>();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddSingleton<INotificationReadRepository, TestNotificationRepository>();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>()
        );

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<NotificationsPage>(
                ParameterView.Empty
            );
            return component.ToHtmlString();
        });

        html.ShouldContain("Notification type");
        html.ShouldContain("All notification types");

        var tableStart = html.IndexOf("<table", StringComparison.Ordinal);
        var tableEnd = html.IndexOf("</table>", StringComparison.Ordinal);
        tableStart.ShouldBeGreaterThanOrEqualTo(0);
        tableEnd.ShouldBeGreaterThan(tableStart);
        var table = html[tableStart..tableEnd];
        table.ShouldContain("Severity");
        table.ShouldContain("Warning");
        table.ShouldContain("Files offline");
        table.ShouldNotContain("Legacy notification");
    }

    private sealed class TestNotificationRepository : INotificationReadRepository
    {
        public Task<int> CountUnresolvedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<PagedResult<NotificationReadModel>> SearchAsync(
            NotificationSearchQuery query,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                new PagedResult<NotificationReadModel>(
                    Items:
                    [
                        new NotificationReadModel(
                            NotificationId: 1,
                            CreatedAt: DateTime.UnixEpoch,
                            ResolvedAt: null,
                            NotificationSeverity: NotificationSeverity.Warning,
                            NotificationKind: NotificationKind.Legacy,
                            Message: "Files offline",
                            RelatedEntity: null
                        ),
                    ],
                    TotalCount: 1,
                    PageIndex: query.PageIndex,
                    PageSize: query.PageSize
                )
            );

        public Task<IReadOnlyList<NotificationReadModel>> GetLatestUnresolvedAsync(
            int take,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<NotificationReadModel?> GetByIdAsync(
            int notificationId,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IReadOnlyList<NotificationReadModel>> GetByIdsAsync(
            IReadOnlyList<int> notificationIds,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() =>
            Initialize("http://localhost/", "http://localhost/notifications");
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
