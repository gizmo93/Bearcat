using Bearcat.Abstractions.ConfigurationFields;

namespace Bearcat.Abstractions.RemoteSource.Dto;

public sealed record RemoteSourceDto(
    string Name,
    string ClassName,
    IReadOnlyList<ConfigurationField> ConfigurationFields
);
