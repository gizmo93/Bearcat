namespace Bearcat.Domain.Shared.ConfigurationFields;

public sealed class ConfigurationFieldValidationException(
    string fieldKey,
    ConfigurationFieldValidationError error
) : ArgumentException($"The configuration field '{fieldKey}' is invalid: {error}.")
{
    public string FieldKey { get; } = fieldKey;

    public ConfigurationFieldValidationError Error { get; } = error;
}
