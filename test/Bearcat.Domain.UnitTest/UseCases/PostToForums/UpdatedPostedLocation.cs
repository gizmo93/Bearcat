namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public sealed record UpdatedPostedLocation(
    int PostedLocationId,
    string Url,
    int ForumPostTemplateId
);
