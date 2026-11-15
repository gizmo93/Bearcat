using Bearcat.Abstractions.Media;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Generation;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageReleaseScreenshots.Generation;

public class ScreenshotGridHeaderLinesTest
{
    [Test]
    public void Build_HdrVideoWithTwoAudioStreams_ReturnsFileSizeDurationAndStreamLines()
    {
        // Arrange
        var metadata = new MediaFileMetadata(
            ContainerFormat: "Matroska",
            SizeBytes: 1_610_612_736,
            Duration: new TimeSpan(1, 52, 3),
            VideoStream: CreateVideoStream(
                transferCharacteristics: "PQ",
                hdrFormat: "Dolby Vision / SMPTE ST 2086"
            ),
            AudioStreams:
            [
                CreateAudioStream(
                    codec: "E-AC-3",
                    channelLayout: "L R C LFE Ls Rs",
                    language: "de"
                ),
                CreateAudioStream(codec: "AAC", channelLayout: null, language: "en"),
            ],
            SubtitleStreams: []
        );

        // Act
        var result = ScreenshotGridHeaderLines.Build("Movie.2025.2160p.mkv", metadata);

        // Assert
        result.ShouldBe([
            "Movie.2025.2160p.mkv",
            "Size: 1.5 GB | Duration: 01:52:03",
            "Video: HEVC, 3840x2160, 23.976 fps, Dolby Vision, HDR10 | Audio: E-AC-3 L R C LFE Ls Rs de, AAC 2 ch en",
        ]);
    }

    [Test]
    public void Build_SdrVideoWithoutAudioAndSize_OmitsMissingSegments()
    {
        // Arrange
        var metadata = new MediaFileMetadata(
            ContainerFormat: "MPEG-4",
            SizeBytes: null,
            Duration: TimeSpan.FromSeconds(42),
            VideoStream: CreateVideoStream(transferCharacteristics: "BT.709", hdrFormat: null),
            AudioStreams: [],
            SubtitleStreams: []
        );

        // Act
        var result = ScreenshotGridHeaderLines.Build("clip.mp4", metadata);

        // Assert
        result.ShouldBe(["clip.mp4", "Duration: 00:00:42", "Video: HEVC, 3840x2160, 23.976 fps"]);
    }

    private static MediaVideoStreamMetadata CreateVideoStream(
        string transferCharacteristics,
        string? hdrFormat
    )
    {
        return new MediaVideoStreamMetadata(
            Index: 0,
            Codec: "HEVC",
            CodecProfile: "Main 10",
            IsDefault: true,
            Language: null,
            Title: null,
            Width: 3840,
            Height: 2160,
            Fps: 23.976,
            PixelFormat: null,
            BitrateKbps: null,
            TransferCharacteristics: transferCharacteristics,
            HdrFormat: hdrFormat
        );
    }

    private static MediaAudioStreamMetadata CreateAudioStream(
        string codec,
        string? channelLayout,
        string language
    )
    {
        return new MediaAudioStreamMetadata(
            Index: 1,
            Codec: codec,
            CodecProfile: null,
            IsDefault: false,
            Language: language,
            Title: null,
            SampleRate: 48000,
            ChannelLayout: channelLayout,
            Channels: 2,
            BitrateKbps: null
        );
    }
}
