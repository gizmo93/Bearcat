// See https://aka.ms/new-console-template for more information

using Bearcat.Domain.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.InversionOfControl;
using Bearcat.Hosters.DDownload;
using Bearcat.Hosters.DDownload.ApiClient;
using Bearcat.Hosters.InversionOfControl;
using Bearcat.Infrastructure.InversionOfControl;

var builder = new HostApplicationBuilder();
builder.Services.AddDomain();
builder.Services.AddHosters();
builder.Services.AddInfrastructure(builder.Configuration);

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
