using BearCat.Core.Infrastructure.Database;
using BearCat.Core.InversionOfControl;
using Bearcat.Website;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Host;

public static class HostStartup
{
    public static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddBearcatComponents();

        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddJsonFile("appsettings.user.json", optional: true, reloadOnChange: false);
        }

        builder.Services.AddCore(builder.Configuration);

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        if (app.Environment.IsProduction())
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BearcatDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        await app.RunAsync(cancellationToken);
    }
}
