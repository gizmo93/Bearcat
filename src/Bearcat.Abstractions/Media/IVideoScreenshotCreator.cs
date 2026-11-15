namespace Bearcat.Abstractions.Media;

public interface IVideoScreenshotCreator
{
    Task<IReadOnlyList<CreatedScreenshotFile>> CreateScreenshotsAsync(
        ScreenshotCreationRequest request,
        CancellationToken cancellationToken
    );

    Task<CreatedScreenshotGridFile> CreateScreenshotGridAsync(
        ScreenshotGridCreationRequest request,
        CancellationToken cancellationToken
    );
}
