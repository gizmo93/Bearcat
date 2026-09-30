namespace Bearcat.Abstractions.DistributionSite.Dto;

public sealed record PreparedDraft(string DraftEditorUrl, bool RequiresSameAccountBrowserSession);
