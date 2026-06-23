using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageQualityProfiles.Dto;
using Bearcat.Domain.UseCases.ManageQualityProfiles.Repositories;

namespace Bearcat.Domain.UseCases.ManageQualityProfiles;

public class QualityProfileService(IQualityProfileWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        string name,
        IReadOnlyList<QualityCheckRuleInput> rules,
        CancellationToken cancellationToken = default
    )
    {
        Validate(name);

        var profile = new QualityProfile
        {
            Name = name.Trim(),
            Rules = rules.Select(ToRule).ToList(),
        };

        writeRepository.Add(profile);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return profile.Id;
    }

    public async Task UpdateAsync(
        int qualityProfileId,
        string name,
        IReadOnlyList<QualityCheckRuleInput> rules,
        CancellationToken cancellationToken = default
    )
    {
        Validate(name);

        var profile = await writeRepository.GetByIdAsync(qualityProfileId, cancellationToken);
        profile.Name = name.Trim();
        profile.Rules.Clear();

        foreach (var rule in rules)
        {
            profile.Rules.Add(ToRule(rule));
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int qualityProfileId,
        CancellationToken cancellationToken = default
    )
    {
        var profile = await writeRepository.GetByIdAsync(qualityProfileId, cancellationToken);
        writeRepository.Remove(profile);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private static QualityCheckRule ToRule(QualityCheckRuleInput input)
    {
        return new QualityCheckRule
        {
            RuleType = input.RuleType,
            ParametersJson = input.ParametersJson,
        };
    }

    private static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }
    }
}
