using Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Repositories;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;

namespace Bearcat.Website.Shared.UploadTargets;

public class UploadTargetChoices(
    IReadOnlyList<HosterRegistrationReadModel> hosterRegistrations,
    IReadOnlyList<HosterAccountPoolReadModel> hosterAccountPools
)
{
    private readonly IReadOnlyList<HosterAccountPoolReadModel> hosterAccountPools =
        hosterAccountPools.OrderBy(pool => pool.Name).ToList();

    public static async Task<UploadTargetChoices> LoadAsync(IScopedOperationRunner operationRunner)
    {
        var hosterRegistrations = await operationRunner.RunAsync(
            (IHosterConfigurationReadRepository repository) => repository.GetAllRegistrationsAsync()
        );
        var hosterAccountPools = await operationRunner.RunAsync(
            (IHosterAccountPoolReadRepository repository) => repository.GetAllAsync()
        );

        return new UploadTargetChoices(hosterRegistrations, hosterAccountPools);
    }

    public IReadOnlyList<HosterRegistrationReadModel> GetSelectableHosterRegistrations(
        UploadTarget? selectedUploadTarget,
        UploadSource uploadSource
    )
    {
        return hosterRegistrations
            .Where(registration =>
                (
                    registration.IsActive
                    || registration.Id == selectedUploadTarget?.HosterRegistrationId
                )
                && registration.IsVideoHoster == IsVideoFilesUploadSource(uploadSource)
            )
            .OrderBy(registration => registration.Name)
            .ToList();
    }

    public IReadOnlyList<HosterAccountPoolReadModel> GetSelectableHosterAccountPools(
        UploadSource uploadSource
    )
    {
        return hosterAccountPools
            .Where(pool => pool.IsVideoHoster == IsVideoFilesUploadSource(uploadSource))
            .ToList();
    }

    public IReadOnlyList<UploadTargetHoster> GetHosters()
    {
        var hostersOfRegistrations = hosterRegistrations.Select(
            registration => new UploadTargetHoster(
                registration.FullClassName,
                registration.HosterName,
                registration.IsVideoHoster
            )
        );
        var hostersOfPools = hosterAccountPools.Select(pool => new UploadTargetHoster(
            pool.HosterClassName,
            pool.HosterName,
            pool.IsVideoHoster
        ));

        return hostersOfRegistrations
            .Concat(hostersOfPools)
            .DistinctBy(hoster => hoster.HosterClassName)
            .OrderBy(hoster => hoster.HosterName)
            .ToList();
    }

    public string GetHosterClassName(UploadTarget uploadTarget)
    {
        return uploadTarget.HosterAccountPoolId is { } hosterAccountPoolId
            ? FindHosterAccountPool(hosterAccountPoolId).HosterClassName
            : FindHosterRegistration(uploadTarget.HosterRegistrationId!.Value).FullClassName;
    }

    public bool IsUsableForUploads(UploadTarget uploadTarget)
    {
        if (uploadTarget.HosterAccountPoolId is { } hosterAccountPoolId)
        {
            return FindHosterAccountPool(hosterAccountPoolId)
                .MembersInPriorityOrder.Any(member =>
                    member.IsActive
                    && !member.HasUnreadableSecrets
                    && !member.RequiresCaptchaVerification
                );
        }

        var hosterRegistration = FindHosterRegistration(uploadTarget.HosterRegistrationId!.Value);

        return hosterRegistration.IsActive
            && !hosterRegistration.HasUnreadableSecrets
            && !hosterRegistration.RequiresCaptchaVerification;
    }

    public bool MatchesUploadSource(UploadTarget uploadTarget, UploadSource uploadSource)
    {
        var isVideoHoster = uploadTarget.HosterAccountPoolId is { } hosterAccountPoolId
            ? FindHosterAccountPool(hosterAccountPoolId).IsVideoHoster
            : FindHosterRegistration(uploadTarget.HosterRegistrationId!.Value).IsVideoHoster;

        return isVideoHoster == IsVideoFilesUploadSource(uploadSource);
    }

    public string GetName(UploadTarget uploadTarget)
    {
        return uploadTarget.HosterAccountPoolId is { } hosterAccountPoolId
            ? FindHosterAccountPool(hosterAccountPoolId).Name
            : FindHosterRegistration(uploadTarget.HosterRegistrationId!.Value).Name;
    }

    public bool SupportsPremiumOnlyDownloads(UploadTarget? uploadTarget)
    {
        return uploadTarget switch
        {
            null => false,
            { HosterAccountPoolId: { } hosterAccountPoolId } => FindHosterAccountPool(
                hosterAccountPoolId
            ).SupportsPremiumOnlyDownloads,
            _ => FindHosterRegistration(
                uploadTarget.HosterRegistrationId!.Value
            ).SupportsPremiumOnlyDownloads,
        };
    }

    public int? GetMaxFileSizeMb(UploadTarget? uploadTarget)
    {
        return uploadTarget switch
        {
            null => null,
            { HosterAccountPoolId: { } hosterAccountPoolId } => FindHosterAccountPool(
                hosterAccountPoolId
            ).MaxFileSizeMb,
            _ => FindHosterRegistration(uploadTarget.HosterRegistrationId!.Value).MaxFileSizeMb,
        };
    }

    public static bool HasActiveMember(HosterAccountPoolReadModel hosterAccountPool)
    {
        return hosterAccountPool.MembersInPriorityOrder.Any(member => member.IsActive);
    }

    private HosterRegistrationReadModel FindHosterRegistration(int hosterRegistrationId)
    {
        return hosterRegistrations.First(registration => registration.Id == hosterRegistrationId);
    }

    private HosterAccountPoolReadModel FindHosterAccountPool(int hosterAccountPoolId)
    {
        return hosterAccountPools.First(pool => pool.Id == hosterAccountPoolId);
    }

    private static bool IsVideoFilesUploadSource(UploadSource uploadSource)
    {
        return uploadSource == UploadSource.VideoFiles;
    }
}
