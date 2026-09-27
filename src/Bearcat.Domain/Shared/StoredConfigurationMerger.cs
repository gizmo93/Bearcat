using Bearcat.Abstractions.Security;
using Bearcat.Domain.Shared.Entities;

namespace Bearcat.Domain.Shared;

public static class StoredConfigurationMerger
{
    public static Dictionary<string, string> MergeSubmittedIntoStoredConfiguration(
        IEntityWithEncryptedSecrets registration,
        IReadOnlyDictionary<string, string> submittedConfiguration,
        Func<string, IReadOnlyDictionary<string, string>> deserializeStoredConfiguration,
        ISecretProtector secretProtector
    )
    {
        var mergedConfiguration = registration.HasUnreadableSecrets
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(
                deserializeStoredConfiguration(
                    secretProtector.Unprotect(registration.GetEncryptedSecrets())
                )
            );

        foreach (var (key, value) in submittedConfiguration)
        {
            mergedConfiguration[key] = value;
        }

        return mergedConfiguration;
    }
}
