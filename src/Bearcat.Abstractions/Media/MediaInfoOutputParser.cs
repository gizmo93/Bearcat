using System.Globalization;
using System.Text.Json;

namespace Bearcat.Abstractions.Media;

public static class MediaInfoOutputParser
{
    public static MediaFileMetadata? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (
                !document.RootElement.TryGetProperty("media", out var media)
                || !media.TryGetProperty("track", out var tracks)
                || tracks.ValueKind != JsonValueKind.Array
            )
            {
                return null;
            }

            string? containerFormat = null;
            long? sizeBytes = null;
            TimeSpan? duration = null;
            MediaVideoStreamMetadata? videoStream = null;
            var audioStreams = new List<MediaAudioStreamMetadata>();
            var subtitleStreams = new List<MediaSubtitleStreamMetadata>();

            foreach (var track in tracks.EnumerateArray())
            {
                switch (GetString(track, "@type"))
                {
                    case "General":
                        containerFormat = GetString(track, "Format");
                        sizeBytes = ParseLong(GetString(track, "FileSize"));
                        duration = ParseDuration(GetString(track, "Duration"));
                        break;
                    case "Video" when videoStream is null:
                        videoStream = ParseVideoStream(track);
                        break;
                    case "Audio":
                        audioStreams.Add(ParseAudioStream(track));
                        break;
                    case "Text":
                        subtitleStreams.Add(ParseSubtitleStream(track));
                        break;
                }
            }

            return new MediaFileMetadata(
                ContainerFormat: containerFormat,
                SizeBytes: sizeBytes,
                Duration: duration,
                VideoStream: videoStream,
                AudioStreams: audioStreams,
                SubtitleStreams: subtitleStreams
            );
        }
    }

    private static MediaVideoStreamMetadata ParseVideoStream(JsonElement track)
    {
        return new MediaVideoStreamMetadata(
            Index: ParseInt(GetString(track, "StreamOrder")) ?? 0,
            Codec: GetString(track, "Format") ?? "unknown",
            CodecProfile: GetString(track, "Format_Profile"),
            IsDefault: ParseYesNo(GetString(track, "Default")),
            Language: GetString(track, "Language"),
            Title: GetString(track, "Title"),
            Width: ParseInt(GetString(track, "Width")),
            Height: ParseInt(GetString(track, "Height")),
            Fps: ParseDouble(GetString(track, "FrameRate")),
            PixelFormat: ComposePixelFormat(track),
            BitrateKbps: ParseBitrateKbps(track)
        );
    }

    private static MediaAudioStreamMetadata ParseAudioStream(JsonElement track)
    {
        return new MediaAudioStreamMetadata(
            Index: ParseInt(GetString(track, "StreamOrder")) ?? 0,
            Codec: GetString(track, "Format") ?? "unknown",
            CodecProfile: GetString(track, "Format_Commercial_IfAny")
                ?? GetString(track, "Format_AdditionalFeatures"),
            IsDefault: ParseYesNo(GetString(track, "Default")),
            Language: GetString(track, "Language"),
            Title: GetString(track, "Title"),
            SampleRate: ParseInt(GetString(track, "SamplingRate")),
            ChannelLayout: GetString(track, "ChannelLayout"),
            Channels: ParseInt(GetString(track, "Channels")),
            BitrateKbps: ParseBitrateKbps(track)
        );
    }

    private static MediaSubtitleStreamMetadata ParseSubtitleStream(JsonElement track)
    {
        return new MediaSubtitleStreamMetadata(
            Index: ParseInt(GetString(track, "StreamOrder")) ?? 0,
            Codec: GetString(track, "Format") ?? "unknown",
            IsDefault: ParseYesNo(GetString(track, "Default")),
            Forced: ParseYesNo(GetString(track, "Forced")),
            Language: GetString(track, "Language"),
            Title: GetString(track, "Title")
        );
    }

    private static string? ComposePixelFormat(JsonElement track)
    {
        var parts = new List<string>();

        var colorSpace = GetString(track, "ColorSpace");
        if (!string.IsNullOrWhiteSpace(colorSpace))
        {
            parts.Add(colorSpace);
        }

        var chromaSubsampling = GetString(track, "ChromaSubsampling");
        if (!string.IsNullOrWhiteSpace(chromaSubsampling))
        {
            parts.Add(chromaSubsampling);
        }

        var bitDepth = GetString(track, "BitDepth");
        if (!string.IsNullOrWhiteSpace(bitDepth))
        {
            parts.Add($"{bitDepth} bit");
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static int? ParseBitrateKbps(JsonElement track)
    {
        var bitrate = ParseInt(GetString(track, "BitRate"));
        return bitrate is > 0 ? bitrate / 1000 : null;
    }

    private static TimeSpan? ParseDuration(string? value)
    {
        var seconds = ParseDouble(value);
        return seconds is > 0 ? TimeSpan.FromSeconds(seconds.Value) : null;
    }

    private static bool ParseYesNo(string? value)
    {
        return string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetString(JsonElement element, string name)
    {
        return
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? Normalize(value.GetString())
            : null;
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var number
        )
            ? number
            : null;
    }

    private static long? ParseLong(string? value)
    {
        return long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var number
        )
            ? number
            : null;
    }

    private static double? ParseDouble(string? value)
    {
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number
        )
            ? number
            : null;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
