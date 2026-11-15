namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public class ImageUploadConfigTemplateFormModel
{
    public string? Name { get; set; }

    public int? ImageHosterRegistrationId { get; set; }

    public bool UploadCover { get; set; } = true;

    public bool UploadScreenshots { get; set; }

    public bool UploadScreenshotGrid { get; set; }
}
