namespace Bearcat.Api.Contracts.PostedLocations;

public record AddPostedLocationRequest
{
    /// <summary>
    /// Absolute http or https URL of the post. Surrounding whitespace is removed.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The distribution site registration the release was posted to.
    /// </summary>
    public int? DistributionSiteRegistrationId { get; init; }

    /// <summary>
    /// The forum post template used to render the post.
    /// </summary>
    public int? ForumPostTemplateId { get; init; }
}
