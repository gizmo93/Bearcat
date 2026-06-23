namespace Bearcat.Domain.Shared.QualityGate;

public sealed record QualityCheckParameterDescriptor(
    string Key,
    QualityCheckParameterKind Kind,
    object DefaultValue,
    string LabelKey,
    string? HelperTextKey = null,
    string? Placeholder = null,
    int? Minimum = null
);
