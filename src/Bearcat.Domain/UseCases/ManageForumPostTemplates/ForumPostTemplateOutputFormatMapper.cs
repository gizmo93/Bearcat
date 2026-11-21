using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates;

public static class ForumPostTemplateOutputFormatMapper
{
    public static ForumPostTemplateOutputFormat MapFromContentFormat(
        PostContentFormat contentFormat
    )
    {
        return contentFormat switch
        {
            PostContentFormat.BBCode => ForumPostTemplateOutputFormat.BBCode,
            PostContentFormat.Html => ForumPostTemplateOutputFormat.Html,
            PostContentFormat.PlainText => ForumPostTemplateOutputFormat.PlainText,
            _ => throw new ArgumentOutOfRangeException(
                nameof(contentFormat),
                contentFormat,
                "No forum post template output format exists for this content format."
            ),
        };
    }
}
