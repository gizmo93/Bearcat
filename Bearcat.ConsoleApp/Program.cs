// See https://aka.ms/new-console-template for more information

using BearCat.Core;
using BearCat.Core.Domain.Abstractions;
using BearCat.Core.Domain.Abstractions.Archiver;
using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Infrastructure.Archivers._7Zip;
using BearCat.Core.Infrastructure.Archivers.Rar;
using BearCat.Core.Infrastructure.Hosters;
using BearCat.Core.Infrastructure.Hosters.Rapidgator;
using BearCat.Core.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = new HostApplicationBuilder();
builder.Services.AddCore(builder.Configuration);

var app = builder.Build();

await app.StartAsync();

await using var scope = app.Services.CreateAsyncScope();

var archivers = scope.ServiceProvider.GetServices<IArchiver>();
var rarArchiver = archivers.OfType<SevenZipArchiver>().First();

var sourceFolder = "/Volumes/Samsung 980/Test/TestFolder";
var outputFolder = "/Volumes/Samsung 980/Test";

var result = await rarArchiver.ArchiveAsync(
    sourceFolderPath: sourceFolder,
    destinationPath: outputFolder,
    archiveNamePrefix: "Video",
    targetFileSizeMb: 300,
    password: "SuperSafePassword123",
    cancellationToken: CancellationToken.None);

await app.StopAsync();
