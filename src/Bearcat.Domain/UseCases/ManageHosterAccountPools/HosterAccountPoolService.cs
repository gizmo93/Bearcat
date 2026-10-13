using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Deletion;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Dto;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Repositories;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Validation;

namespace Bearcat.Domain.UseCases.ManageHosterAccountPools;

public class HosterAccountPoolService(
    IHosterAccountPoolReadRepository readRepository,
    IHosterAccountPoolWriteRepository writeRepository,
    IHosterFactory hosterFactory
)
{
    public const int NameMaxLength = 200;

    public async Task<IReadOnlyList<PoolableHosterClassReadModel>> GetPoolableHosterClassesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var hostersByName = hosterFactory.GetHostersByName();
        var registeredHosterClassNames =
            await readRepository.GetHosterClassNamesWithRegistrationsAsync(cancellationToken);

        return registeredHosterClassNames
            .Where(hosterClassName => hostersByName[hosterClassName] is IHosterWithStorageQuota)
            .Select(hosterClassName => new PoolableHosterClassReadModel(
                hosterClassName,
                hostersByName[hosterClassName].Name
            ))
            .OrderBy(hosterClass => hosterClass.HosterName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<HosterAccountPoolSaveResult> CreateAsync(
        string hosterClassName,
        HosterAccountPoolInput input,
        CancellationToken cancellationToken = default
    )
    {
        EnsureHosterSupportsStorageQuota(hosterClassName);

        var normalizedInput = Normalize(input);
        var validationErrors = await ValidateAsync(
            normalizedInput,
            excludedHosterAccountPoolId: null,
            cancellationToken
        );

        if (validationErrors.Count > 0)
        {
            return HosterAccountPoolSaveResult.Invalid(validationErrors);
        }

        await EnsureMembersBelongToHosterAsync(
            hosterClassName,
            normalizedInput.MemberHosterRegistrationIdsInPriorityOrder,
            cancellationToken
        );

        var hosterAccountPool = new HosterAccountPool
        {
            Name = normalizedInput.Name,
            HosterClassName = hosterClassName,
            MinimumFreeSpaceBytes = normalizedInput.MinimumFreeSpaceBytes,
        };
        ApplyMembers(hosterAccountPool, normalizedInput.MemberHosterRegistrationIdsInPriorityOrder);

        writeRepository.Add(hosterAccountPool);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return HosterAccountPoolSaveResult.Saved(hosterAccountPool.Id);
    }

    public async Task<HosterAccountPoolSaveResult> UpdateAsync(
        int hosterAccountPoolId,
        HosterAccountPoolInput input,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedInput = Normalize(input);
        var hosterAccountPool = await writeRepository.GetByIdAsync(
            hosterAccountPoolId,
            cancellationToken
        );
        var validationErrors = await ValidateAsync(
            normalizedInput,
            hosterAccountPoolId,
            cancellationToken
        );

        if (validationErrors.Count > 0)
        {
            return HosterAccountPoolSaveResult.Invalid(validationErrors);
        }

        await EnsureMembersBelongToHosterAsync(
            hosterAccountPool.HosterClassName,
            normalizedInput.MemberHosterRegistrationIdsInPriorityOrder,
            cancellationToken
        );

        hosterAccountPool.Name = normalizedInput.Name;
        hosterAccountPool.MinimumFreeSpaceBytes = normalizedInput.MinimumFreeSpaceBytes;
        ApplyMembers(hosterAccountPool, normalizedInput.MemberHosterRegistrationIdsInPriorityOrder);

        await writeRepository.SaveChangesAsync(cancellationToken);

        return HosterAccountPoolSaveResult.Saved(hosterAccountPoolId);
    }

    public async Task<HosterAccountPoolDeleteResult> DeleteAsync(
        int hosterAccountPoolId,
        CancellationToken cancellationToken = default
    )
    {
        var usage = await writeRepository.GetUsageAsync(hosterAccountPoolId, cancellationToken);

        if (usage.IsInUse)
        {
            return new HosterAccountPoolDeleteResult(IsDeleted: false, usage);
        }

        var hosterAccountPool = await writeRepository.GetByIdAsync(
            hosterAccountPoolId,
            cancellationToken
        );
        writeRepository.Remove(hosterAccountPool);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return new HosterAccountPoolDeleteResult(IsDeleted: true, usage);
    }

    private static HosterAccountPoolInput Normalize(HosterAccountPoolInput input)
    {
        return input with { Name = input.Name.Trim() };
    }

    private async Task<List<HosterAccountPoolValidationError>> ValidateAsync(
        HosterAccountPoolInput normalizedInput,
        int? excludedHosterAccountPoolId,
        CancellationToken cancellationToken
    )
    {
        var validationErrors = new List<HosterAccountPoolValidationError>();

        if (normalizedInput.Name.Length == 0)
        {
            validationErrors.Add(HosterAccountPoolValidationError.NameRequired);
        }
        else if (normalizedInput.Name.Length > NameMaxLength)
        {
            validationErrors.Add(HosterAccountPoolValidationError.NameTooLong);
        }
        else if (
            await writeRepository.NameExistsAsync(
                normalizedInput.Name,
                excludedHosterAccountPoolId,
                cancellationToken
            )
        )
        {
            validationErrors.Add(HosterAccountPoolValidationError.NameAlreadyExists);
        }

        if (normalizedInput.MinimumFreeSpaceBytes < 0)
        {
            validationErrors.Add(HosterAccountPoolValidationError.MinimumFreeSpaceNegative);
        }

        return validationErrors;
    }

    private void EnsureHosterSupportsStorageQuota(string hosterClassName)
    {
        var hoster = hosterFactory.GetByName(hosterClassName);

        if (hoster is not IHosterWithStorageQuota)
        {
            throw new InvalidOperationException(
                $"Hoster {hoster.Name} does not support storage quota checks and cannot be pooled."
            );
        }
    }

    private async Task EnsureMembersBelongToHosterAsync(
        string hosterClassName,
        IReadOnlyList<int> memberHosterRegistrationIds,
        CancellationToken cancellationToken
    )
    {
        if (memberHosterRegistrationIds.Distinct().Count() != memberHosterRegistrationIds.Count)
        {
            throw new ArgumentException(
                "A hoster registration can only be added once to a hoster account pool.",
                nameof(memberHosterRegistrationIds)
            );
        }

        var hosterClassNamesByHosterRegistrationId =
            await writeRepository.GetHosterClassNamesByHosterRegistrationIdAsync(
                memberHosterRegistrationIds,
                cancellationToken
            );

        foreach (var hosterRegistrationId in memberHosterRegistrationIds)
        {
            var memberHosterClassName = hosterClassNamesByHosterRegistrationId[
                hosterRegistrationId
            ];

            if (memberHosterClassName != hosterClassName)
            {
                throw new InvalidOperationException(
                    $"Hoster registration {hosterRegistrationId} belongs to hoster {memberHosterClassName} and cannot be added to a pool for hoster {hosterClassName}."
                );
            }
        }
    }

    private static void ApplyMembers(
        HosterAccountPool hosterAccountPool,
        IReadOnlyList<int> memberHosterRegistrationIdsInPriorityOrder
    )
    {
        hosterAccountPool.Members.RemoveAll(member =>
            !memberHosterRegistrationIdsInPriorityOrder.Contains(member.HosterRegistrationId)
        );

        for (var index = 0; index < memberHosterRegistrationIdsInPriorityOrder.Count; index++)
        {
            var hosterRegistrationId = memberHosterRegistrationIdsInPriorityOrder[index];
            var member = hosterAccountPool.Members.Find(existingMember =>
                existingMember.HosterRegistrationId == hosterRegistrationId
            );

            if (member is null)
            {
                member = new HosterAccountPoolMember
                {
                    HosterRegistrationId = hosterRegistrationId,
                };
                hosterAccountPool.Members.Add(member);
            }

            member.Priority = index + 1;
        }
    }
}
