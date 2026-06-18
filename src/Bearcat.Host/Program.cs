using Bearcat.Application.InversionOfControl;
using Bearcat.Archivers.InversionOfControl;
using Bearcat.Domain.InversionOfControl;
using Bearcat.Host;
using Bearcat.Hosters.InversionOfControl;
using Bearcat.ImageHosters.InversionOfControl;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.InversionOfControl;
using Bearcat.Infrastructure.Security;
using Bearcat.LinkCrypters.InversionOfControl;
using Bearcat.NfoDatabases.InversionOfControl;
using Bearcat.SeriesDatabases.InversionOfControl;
using Bearcat.Website;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.EventLog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();
if (OperatingSystem.IsWindows())
{
#pragma warning disable CA1416 // Validate platform compatibility
    builder.Services.Configure<EventLogSettings>(settings => settings.SourceName = "Bearcat");
#pragma warning restore CA1416
}

var isDesktopMode = builder.Configuration.GetValue("Bearcat:DesktopMode", false);

builder.Services.AddBearcatBlueprintComponents();
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
});

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var isRunningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase
);

if (!isRunningInContainer)
{
    builder.Configuration.AddJsonFile(
        "appsettings.user.json",
        optional: true,
        reloadOnChange: false
    );

    // When running a Windows service, we set the Bearcat:DataDirectory (the path, where the bearcat.key file is located)
    // to the same as where the config.json is
    if (File.Exists(BearcatPaths.WindowsServiceConfigPath))
    {
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Bearcat:DataDirectory"] = Path.GetDirectoryName(
                    BearcatPaths.WindowsServiceConfigPath
                ),
            }
        );

        builder.Configuration.AddJsonFile(
            BearcatPaths.WindowsServiceConfigPath,
            optional: true,
            reloadOnChange: false
        );
    }
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDomain();
builder.Services.AddHosters();
builder.Services.AddImageHosters();
builder.Services.AddArchivers();
builder.Services.AddLinkCrypters();
builder.Services.AddNfoDatabases();
builder.Services.AddSeriesDatabases();

var app = builder.Build();

await app.Services.GetRequiredService<IEncryptionKeyProvider>().InitializeAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!isDesktopMode)
{
    app.UseHttpsRedirection();
}

var supportedCultures = new[] { "en-US", "de-DE" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

app.MapGet(
    "/culture/set",
    (string culture, string redirectUri, HttpContext context) =>
    {
        if (supportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
        {
            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    Path = "/",
                    SameSite = SameSiteMode.Lax,
                }
            );
        }

        var localRedirect = Uri.IsWellFormedUriString(redirectUri, UriKind.Relative)
            ? redirectUri
            : "/";

        return Results.LocalRedirect(localRedirect);
    }
);

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

if (app.Environment.IsProduction() || isDesktopMode)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BearcatDbContext>();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();
