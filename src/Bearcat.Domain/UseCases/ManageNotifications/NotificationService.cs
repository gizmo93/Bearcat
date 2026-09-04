using System.Linq.Expressions;
using System.Reflection;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageNotifications;

public class NotificationService(
    INotificationRepository repository,
    TimeProvider timeProvider,
    IApplicationConfigurationProvider configurationProvider
) : INotificationService
{
    public async Task CreateAsync(
        NotificationKind kind,
        string message,
        CancellationToken cancellationToken
    )
    {
        if (IsEnabled(kind))
        {
            repository.Add(CreateNotification(kind, message));
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveAsync(
        int notificationId,
        CancellationToken cancellationToken = default
    )
    {
        await repository.ResolveAsync(
            notificationId: notificationId,
            resolvedAt: timeProvider.GetLocalNow(),
            cancellationToken: cancellationToken
        );
    }

    public async Task ResolveAllAsync(CancellationToken cancellationToken = default)
    {
        await repository.ResolveAllAsync(timeProvider.GetLocalNow(), cancellationToken);
    }

    public void Create<TEntity>(
        NotificationKind kind,
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    )
    {
        if (!IsEnabled(kind))
        {
            return;
        }

        var notification = CreateNotification(kind, message);

        var member = (MemberExpression)selector.Body;
        var property = (PropertyInfo)member.Member;
        property.SetValue(obj: notification, value: entity, index: null);

        repository.Add(notification);
    }

    private Notification CreateNotification(NotificationKind kind, string message)
    {
        var definition = NotificationDefinitions.Get(kind);

        return new Notification
        {
            NotificationKind = kind,
            NotificationSeverity = definition.Severity,
            Message = message,
            CreatedAt = timeProvider.GetLocalNow(),
        };
    }

    private bool IsEnabled(NotificationKind kind)
    {
        NotificationDefinitions.Get(kind);
        var configuration = configurationProvider.GetConfiguration<NotificationConfiguration>();

        return kind switch
        {
            NotificationKind.ReleaseAutomaticallyCreated =>
                configuration.ReleaseAutomaticallyCreated,
            NotificationKind.ReleaseFolderMissing => configuration.ReleaseFolderMissing,
            NotificationKind.ArchiveCreationFailed => configuration.ArchiveCreationFailed,
            NotificationKind.ArchiveFilesMissing => configuration.ArchiveFilesMissing,
            NotificationKind.InitialUploadCreated => configuration.InitialUploadCreated,
            NotificationKind.UploadCompleted => configuration.UploadCompleted,
            NotificationKind.UploadFailed => configuration.UploadFailed,
            NotificationKind.UploadCancellationRequested =>
                configuration.UploadCancellationRequested,
            NotificationKind.UploadCanceled => configuration.UploadCanceled,
            NotificationKind.FilesOffline => configuration.FilesOffline,
            NotificationKind.UploadMarkedOffline => configuration.UploadMarkedOffline,
            NotificationKind.HosterStatusCheckFailed => configuration.HosterStatusCheckFailed,
            NotificationKind.AutomaticReuploadCreated => configuration.AutomaticReuploadCreated,
            NotificationKind.CaptchaVerificationRequired =>
                configuration.CaptchaVerificationRequired,
            NotificationKind.LinkCrypterContainerCreationFailed =>
                configuration.LinkCrypterContainerCreationFailed,
            NotificationKind.LinkCrypterContainerUpdateFailed =>
                configuration.LinkCrypterContainerUpdateFailed,
            NotificationKind.CollectionLinkCrypterContainerInvalid =>
                configuration.CollectionLinkCrypterContainerInvalid,
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(kind),
                actualValue: kind,
                message: "Notifications must use a defined notification kind."
            ),
        };
    }
}
