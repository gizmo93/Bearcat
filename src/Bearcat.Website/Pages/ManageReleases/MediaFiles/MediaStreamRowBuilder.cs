using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Microsoft.Extensions.Localization;

namespace Bearcat.Website.Pages.ManageReleases.MediaFiles;

public static class MediaStreamRowBuilder
{
    public static IReadOnlyList<MediaStreamRow> Build(
        ReleaseMediaFileReadModel file,
        IStringLocalizer localizer
    )
    {
        var rows = new List<MediaStreamRow>();

        if (file.VideoStream is { } video)
        {
            rows.Add(
                new MediaStreamRow(
                    Kind: localizer["Video"],
                    Codec: FormatCodec(video.Codec, video.CodecProfile),
                    Language: ValueOrDash(video.Language),
                    Title: ValueOrDash(video.Title),
                    Details: JoinDetails(
                        FormatResolution(video.Width, video.Height),
                        video.PixelFormat,
                        video.Fps is null ? null : $"{video.Fps} fps",
                        FormatBitrate(video.BitrateKbps)
                    ),
                    IsDefault: video.IsDefault,
                    Forced: false
                )
            );
        }

        foreach (var audio in file.AudioStreams)
        {
            rows.Add(
                new MediaStreamRow(
                    Kind: localizer["Audio"],
                    Codec: FormatCodec(audio.Codec, audio.CodecProfile),
                    Language: ValueOrDash(audio.Language),
                    Title: ValueOrDash(audio.Title),
                    Details: JoinDetails(
                        audio.ChannelLayout,
                        audio.Channels is null ? null : $"{audio.Channels} ch",
                        audio.SampleRate is null ? null : $"{audio.SampleRate} Hz",
                        FormatBitrate(audio.BitrateKbps)
                    ),
                    IsDefault: audio.IsDefault,
                    Forced: false
                )
            );
        }

        foreach (var subtitle in file.SubtitleStreams)
        {
            rows.Add(
                new MediaStreamRow(
                    Kind: localizer["Subtitle"],
                    Codec: subtitle.Codec,
                    Language: ValueOrDash(subtitle.Language),
                    Title: ValueOrDash(subtitle.Title),
                    Details: "-",
                    IsDefault: subtitle.IsDefault,
                    Forced: subtitle.Forced
                )
            );
        }

        return rows;
    }

    private static string FormatCodec(string codec, string? profile) =>
        string.IsNullOrWhiteSpace(profile) ? codec : $"{codec} ({profile})";

    private static string FormatResolution(int? width, int? height) =>
        width is null || height is null ? string.Empty : $"{width}x{height}";

    private static string? FormatBitrate(int? bitrateKbps) =>
        bitrateKbps is null or 0 ? null : $"{bitrateKbps} kbps";

    private static string JoinDetails(params string?[] parts)
    {
        var joined = string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(joined) ? "-" : joined;
    }

    private static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;
}
