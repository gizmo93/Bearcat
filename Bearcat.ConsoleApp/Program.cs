// See https://aka.ms/new-console-template for more information

using BearCat.Core;
using BearCat.Core.Hosters.Rapidgator;
using BearCat.Core.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = new HostApplicationBuilder();
builder.Services.AddCore();

var app = builder.Build();

await app.StartAsync();

await using var scope = app.Services.CreateAsyncScope();

var config = new RapidgatorConfig
{
    Username = "REDACTED_EMAIL",
    Password = "REDACTED_SECRET"
};

var rapidgator = scope.ServiceProvider.GetRequiredService<Rapidgator>();

var result = await rapidgator.UploadFileAsync(
    config,
    "/Users/gizmo_/Downloads/1GB.bin",
    CancellationToken.None);

await app.StopAsync();


async Task WriteFileAsync(string name)
{
    await using var writeStream = File.OpenWrite($"/Users/gizmo_/Downloads/{name}");
    await using var writer = new StreamWriter(writeStream);
    await writer.WriteAsync(name);
}