using Bearcat.Application.InversionOfControl;
using Bearcat.Archivers.InversionOfControl;
using Bearcat.Domain.InversionOfControl;
using Bearcat.Hosters.InversionOfControl;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.InversionOfControl;
using Bearcat.LinkCrypters.InversionOfControl;
using Bearcat.Website;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDomain();
builder.Services.AddHosters();
builder.Services.AddArchivers();
builder.Services.AddLinkCrypters();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

var supportedCultures = new[] { "en-US", "de-DE" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

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

if (app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BearcatDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();
