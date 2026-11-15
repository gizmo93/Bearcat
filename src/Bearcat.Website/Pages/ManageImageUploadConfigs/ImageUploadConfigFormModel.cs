namespace Bearcat.Website.Pages.ManageImageUploadConfigs;

public class ImageUploadConfigFormModel
{
    public string? Name { get; set; }

    public int? ImageHosterRegistrationId { get; set; }

    public bool UploadCover { get; set; } = true;

    public bool UploadScreenshots { get; set; }

    public bool UploadScreenshotGrid { get; set; }
}
