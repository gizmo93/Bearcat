using System.Linq.Expressions;
using Bearcat.Domain.Entities;

namespace Bearcat.Infrastructure.Database.Repositories;

internal static class UploadTargetExpressions
{
    public static readonly Expression<
        Func<UploadConfig, bool>
    > UploadConfigHasActiveHosterRegistration = uploadConfig =>
        uploadConfig.HosterRegistrationId != null
            ? uploadConfig.HosterRegistration!.IsActive
            : uploadConfig.HosterAccountPool!.Members.Any(member =>
                member.HosterRegistration.IsActive
            );
}
