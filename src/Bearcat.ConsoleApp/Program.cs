// See https://aka.ms/new-console-template for more information

using Bearcat.Domain.InversionOfControl;
using Bearcat.Domain.Shared;
using Bearcat.Hosters.InversionOfControl;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.InversionOfControl;
using Microsoft.EntityFrameworkCore;

var builder = new HostApplicationBuilder();
builder.Services.AddDomain();
builder.Services.AddHosters();
builder.Services.AddInfrastructure(builder.Configuration);

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var app = builder.Build();

await app.StartAsync();

await using var scope = app.Services.CreateAsyncScope();

var dbContext = scope.ServiceProvider.GetRequiredService<IBearcatWriteDbContext>();
var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

var upload = await dbContext.Uploads.FirstAsync(
    u => u.Id == 139,
    cancellationToken: CancellationToken.None
);

notificationService.CreateInfo("Test", upload, n => n.Upload);

await dbContext.SaveChangesAsync(CancellationToken.None);

await app.StopAsync();
