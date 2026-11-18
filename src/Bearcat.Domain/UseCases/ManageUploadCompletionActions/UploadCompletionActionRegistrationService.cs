using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.Security;
using Bearcat.Abstractions.UploadCompletionAction;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ConfigurationFields;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets;
using Bearcat.Domain.UseCases.ManageUploadCompletionActions.Repositories;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageUploadCompletionActions;

public class UploadCompletionActionRegistrationService(
    IUploadCompletionActionRegistrationWriteRepository writeRepository,
    IUploadCompletionActionFactory uploadCompletionActionFactory,
    ISecretProtector secretProtector,
    UnreadableSecretsNotificationService unreadableSecretsNotificationService,
    TimeProvider timeProvider
)
{
    public async Task<int> CreateAsync(
        string name,
        string actionClassName,
        IReadOnlyDictionary<string, object?> values,
        bool runForInitialUpload,
        bool runForReupload,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedName = NormalizeName(name);
        var action = uploadCompletionActionFactory.GetByClassName(actionClassName);

        var normalizedValues = ConfigurationValueNormalizer.Normalize(
            action.ConfigurationFields,
            values
        );

        var registration = new UploadCompletionActionRegistration
        {
            Name = normalizedName,
            ActionClassName = actionClassName,
            SerializedConfig = EncryptConfig(normalizedValues),
            IsActive = true,
            RunForInitialUpload = runForInitialUpload,
            RunForReupload = runForReupload,
            CreatedAt = timeProvider.GetLocalNow(),
        };

        writeRepository.Add(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return registration.Id;
    }

    public async Task UpdateAsync(
        int id,
        string name,
        IReadOnlyDictionary<string, object?> values,
        bool runForInitialUpload,
        bool runForReupload,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedName = NormalizeName(name);
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var action = uploadCompletionActionFactory.GetByClassName(registration.ActionClassName);

        var normalizedValues = ConfigurationValueNormalizer.Normalize(
            fields: action.ConfigurationFields,
            submittedValues: values,
            existingValues: registration.HasUnreadableSecrets
                ? null
                : DecryptConfig(action, registration).ToDictionary()
        );

        registration.Name = normalizedName;
        registration.RunForInitialUpload = runForInitialUpload;
        registration.RunForReupload = runForReupload;
        registration.SerializedConfig = EncryptConfig(normalizedValues);
        registration.HasUnreadableSecrets = false;

        await writeRepository.SaveChangesAsync(cancellationToken);

        await unreadableSecretsNotificationService.ResolveNotificationWhenNoUnreadableSecretsRemainAsync(
            cancellationToken
        );
    }

    public async Task<IReadOnlyDictionary<string, object?>> GetConfigValuesWithoutSecretsAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var action = uploadCompletionActionFactory.GetByClassName(registration.ActionClassName);

        var passwordKeys = action
            .ConfigurationFields.Where(field => field.Type == ConfigurationFieldType.Password)
            .Select(field => field.Key)
            .ToHashSet(StringComparer.Ordinal);

        return DecryptConfig(action, registration)
            .ToDictionary()
            .Where(entry => !passwordKeys.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    public async Task ToggleIsActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        registration.IsActive = !registration.IsActive;
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        writeRepository.Remove(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private IUploadCompletionActionConfig DecryptConfig(
        IUploadCompletionAction action,
        UploadCompletionActionRegistration registration
    )
    {
        return action.DeserializeConfig(secretProtector.Unprotect(registration.SerializedConfig));
    }

    private string EncryptConfig(IReadOnlyDictionary<string, object?> values)
    {
        return secretProtector.Protect(ConfigurationValueSerializer.Serialize(values));
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.");
        }

        return name.Trim();
    }
}
