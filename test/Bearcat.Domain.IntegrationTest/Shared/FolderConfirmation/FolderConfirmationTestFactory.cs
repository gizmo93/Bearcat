using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.FolderConfirmation;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Bearcat.Domain.IntegrationTest.Shared.FolderConfirmation;

public static class FolderConfirmationTestFactory
{
    public static async Task ConfirmFolderAsync(BearcatDbContext dbContext, string rootPath)
    {
        var markerId = Guid.NewGuid();
        await File.WriteAllTextAsync(
            FolderConfirmationMarkerFile.GetFilePath(rootPath),
            markerId.ToString()
        );
        dbContext.ConfirmedFolders.Add(
            new ConfirmedFolder
            {
                Path = rootPath,
                MarkerId = markerId,
                ConfirmedAt = DateTime.UtcNow,
            }
        );
        await dbContext.SaveChangesAsync();
    }

    public static async Task<List<Notification>> GetFolderNotConfirmedNotificationsAsync(
        BearcatDbContext dbContext
    )
    {
        return await dbContext
            .Notifications.Where(notification =>
                notification.NotificationKind == NotificationKind.FolderNotConfirmed
            )
            .ToListAsync();
    }

    public static FolderConfirmationCheck CreateCheck(
        BearcatDbContext dbContext,
        WorkingDirectoriesConfig workingDirectoriesConfig
    )
    {
        return new FolderConfirmationCheck(
            new ConfirmableFolderRootProvider(Options.Create(workingDirectoriesConfig)),
            new ConfirmedFolderRepository(dbContext, dbContext),
            new FileSystemService()
        );
    }

    public static FolderWriteCheck CreateWriteCheck(
        BearcatDbContext dbContext,
        INotificationService notificationService,
        WorkingDirectoriesConfig workingDirectoriesConfig
    )
    {
        return new FolderWriteCheck(
            CreateCheck(dbContext, workingDirectoriesConfig),
            new ConfirmedFolderRepository(dbContext, dbContext),
            notificationService,
            Mock.Of<ILogger<FolderWriteCheck>>()
        );
    }
}
