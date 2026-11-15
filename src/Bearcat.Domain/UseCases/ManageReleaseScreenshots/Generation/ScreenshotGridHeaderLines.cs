using System.Globalization;
using Bearcat.Abstractions.Media;
using Humanizer;

namespace Bearcat.Domain.UseCases.ManageReleaseScreenshots.Generation;

public static class ScreenshotGridHeaderLines
{
    private const string SegmentSeparator = " | ";

    public static List<string> Build(string videoFileName, MediaFileMetadata metadata)
    {
        var headerLines = new List<string> { videoFileName };
        var fileSegments = BuildFileSegments(metadata);
        var streamSegments = BuildStreamSegments(metadata);

        if (fileSegments.Count > 0)
        {
            headerLines.Add(string.Join(SegmentSeparator, fileSegments));
        }

        if (streamSegments.Count > 0)
        {
            headerLines.Add(string.Join(SegmentSeparator, streamSegments));
        }

        return headerLines;
    }

    private static List<string> BuildFileSegments(MediaFileMetadata metadata)
    {
        var segments = new List<string>();

        if (metadata.SizeBytes is { } sizeBytes)
        {
            segments.Add(
                $"Size: {sizeBytes.Bytes().Humanize("0.##", CultureInfo.InvariantCulture)}"
            );
        }

        if (metadata.Duration is { } duration)
        {
            segments.Add($"Duration: {FormatDuration(duration)}");
        }

        return segments;
    }

    private static List<string> BuildStreamSegments(MediaFileMetadata metadata)
    {
        var segments = new List<string>();

        if (metadata.VideoStream is { } videoStream)
        {
            segments.Add($"Video: {FormatVideoStream(videoStream)}");
        }

        if (metadata.AudioStreams.Count > 0)
        {
            var audioStreams = metadata.AudioStreams.Select(FormatAudioStream);
            segments.Add($"Audio: {string.Join(", ", audioStreams)}");
        }

        return segments;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var hours = (int)duration.TotalHours;

        return $"{hours.ToString("00", CultureInfo.InvariantCulture)}:{duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture)}";
    }

    private static string FormatVideoStream(MediaVideoStreamMetadata videoStream)
    {
        var parts = new List<string> { videoStream.Codec };

        if (videoStream is { Width: { } width, Height: { } height })
        {
            parts.Add(
                $"{width.ToString(CultureInfo.InvariantCulture)}x{height.ToString(CultureInfo.InvariantCulture)}"
            );
        }

        if (videoStream.Fps is { } fps)
        {
            parts.Add($"{fps.ToString("0.###", CultureInfo.InvariantCulture)} fps");
        }

        parts.AddRange(GetDynamicRangeLabels(videoStream));

        return string.Join(", ", parts);
    }

    private static List<string> GetDynamicRangeLabels(MediaVideoStreamMetadata videoStream)
    {
        var labels = new List<string>();

        if (videoStream.HdrFormat?.Contains("Dolby Vision", StringComparison.Ordinal) == true)
        {
            labels.Add("Dolby Vision");
        }

        var transferCharacteristic =
            VideoTransferCharacteristicMapping.FromMediaInfoTransferCharacteristics(
                videoStream.TransferCharacteristics
            );

        switch (transferCharacteristic)
        {
            case VideoTransferCharacteristic.PerceptualQuantizer:
                labels.Add("HDR10");
                break;
            case VideoTransferCharacteristic.HybridLogGamma:
                labels.Add("HLG");
                break;
        }

        return labels;
    }

    private static string FormatAudioStream(MediaAudioStreamMetadata audioStream)
    {
        var channelLayout =
            audioStream.ChannelLayout
            ?? (
                audioStream.Channels is { } channels
                    ? $"{channels.ToString(CultureInfo.InvariantCulture)} ch"
                    : null
            );

        string?[] parts = [audioStream.Codec, channelLayout, audioStream.Language];

        return string.Join(" ", parts.Where(part => part is not null));
    }
}
