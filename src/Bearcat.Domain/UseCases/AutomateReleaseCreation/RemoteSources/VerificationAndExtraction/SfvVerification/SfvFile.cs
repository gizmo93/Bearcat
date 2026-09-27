namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.SfvVerification;

public record SfvFile(string FilePath, IReadOnlyList<SfvEntry> Entries);
