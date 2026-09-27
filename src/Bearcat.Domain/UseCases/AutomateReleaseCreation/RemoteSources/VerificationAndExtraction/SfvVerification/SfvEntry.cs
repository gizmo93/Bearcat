namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.SfvVerification;

public record SfvEntry(string FilePath, string RelativeFilePath, uint ExpectedCrc32);
