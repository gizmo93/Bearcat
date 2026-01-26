// See https://aka.ms/new-console-template for more information

using BearCat.Core;
using BearCat.Core.Domain.Abstractions;
using BearCat.Core.Domain.Abstractions.Archiver;
using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Infrastructure.Archivers._7Zip;
using BearCat.Core.Infrastructure.Archivers.Rar;
using BearCat.Core.Infrastructure.Hosters;
using BearCat.Core.Infrastructure.Hosters.DDownload;
using BearCat.Core.Infrastructure.Hosters.DDownload.ApiClient;
using BearCat.Core.Infrastructure.Hosters.Rapidgator;
using BearCat.Core.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = new HostApplicationBuilder();
builder.Services.AddCore(builder.Configuration);

var app = builder.Build();

await app.StartAsync();

await using var scope = app.Services.CreateAsyncScope();

var hoster = scope.ServiceProvider.GetRequiredKeyedService<IHoster>(nameof(DDownload));

var cfg = new DDownloadConfig { ApiKey = "98866i0d44iln26txsfdf" };

var file = new ArchiveFile
{
    Id = 0,
    ArchiveId = 0,
    Archive = new Archive(),
    FullFileName = "/Users/gizmo_/Downloads/Client.php.zip",
    UploadedFiles = new List<UploadedFile>()
};

var result = await hoster!.UploadFileAsync(file, cfg, CancellationToken.None);

await app.StopAsync();
