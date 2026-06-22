using Bearcat.Abstractions.Media;
using Shouldly;

namespace Bearcat.Media.UnitTest;

public class MediaInfoOutputParserTest
{
    private const string MultiTrackJson = """
        {
            "media": {
                "@ref": "/releases/movie.mkv",
                "track": [
                    {
                        "@type": "General",
                        "VideoCount": "1",
                        "AudioCount": "2",
                        "Format": "Matroska",
                        "FileSize": "11811160064",
                        "Duration": "5918.500"
                    },
                    {
                        "@type": "Video",
                        "StreamOrder": "0",
                        "ID": "1",
                        "Format": "HEVC",
                        "Format_Profile": "Main 10",
                        "Format_Level": "5.1",
                        "Width": "3840",
                        "Height": "1600",
                        "FrameRate": "23.976",
                        "ColorSpace": "YUV",
                        "ChromaSubsampling": "4:2:0",
                        "BitDepth": "10",
                        "Default": "Yes",
                        "Forced": "No"
                    },
                    {
                        "@type": "Audio",
                        "@typeorder": "1",
                        "StreamOrder": "1",
                        "Format": "E-AC-3",
                        "Format_Commercial_IfAny": "Dolby Digital Plus",
                        "Channels": "6",
                        "ChannelLayout": "L R C LFE Ls Rs",
                        "SamplingRate": "48000",
                        "BitRate": "640000",
                        "Title": "German",
                        "Language": "de",
                        "Default": "Yes",
                        "Forced": "No"
                    },
                    {
                        "@type": "Audio",
                        "@typeorder": "2",
                        "StreamOrder": "2",
                        "Format": "E-AC-3",
                        "Channels": "6",
                        "ChannelLayout": "L R C LFE Ls Rs",
                        "SamplingRate": "48000",
                        "Language": "en",
                        "Default": "No",
                        "Forced": "No"
                    },
                    {
                        "@type": "Text",
                        "StreamOrder": "3",
                        "Format": "UTF-8",
                        "Language": "de",
                        "Default": "No",
                        "Forced": "Yes"
                    },
                    {
                        "@type": "Image",
                        "Format": "JPEG",
                        "Title": "cover"
                    }
                ]
            }
        }
        """;

    [Test]
    public void Parse_ReadsVideoStreamAndIgnoresImageTrack()
    {
        // Act
        var metadata = MediaInfoOutputParser.Parse(MultiTrackJson);

        // Assert
        metadata.ShouldNotBeNull();
        metadata.VideoStream.ShouldNotBeNull();
        metadata.VideoStream.Codec.ShouldBe("HEVC");
        metadata.VideoStream.CodecProfile.ShouldBe("Main 10");
        metadata.VideoStream.Width.ShouldBe(3840);
        metadata.VideoStream.Height.ShouldBe(1600);
        metadata.VideoStream.Fps.ShouldBe(23.976);
        metadata.VideoStream.PixelFormat.ShouldBe("YUV 4:2:0 10 bit");
        metadata.VideoStream.IsDefault.ShouldBeTrue();
    }

    [Test]
    public void Parse_KeepsAllAudioStreams()
    {
        // Act
        var metadata = MediaInfoOutputParser.Parse(MultiTrackJson);

        // Assert
        metadata.ShouldNotBeNull();
        metadata.AudioStreams.Count.ShouldBe(2);

        var german = metadata.AudioStreams[0];
        german.Codec.ShouldBe("E-AC-3");
        german.CodecProfile.ShouldBe("Dolby Digital Plus");
        german.Language.ShouldBe("de");
        german.ChannelLayout.ShouldBe("L R C LFE Ls Rs");
        german.Channels.ShouldBe(6);
        german.SampleRate.ShouldBe(48000);
        german.BitrateKbps.ShouldBe(640);
        german.IsDefault.ShouldBeTrue();

        var english = metadata.AudioStreams[1];
        english.Language.ShouldBe("en");
        english.BitrateKbps.ShouldBeNull();
        english.IsDefault.ShouldBeFalse();
    }

    [Test]
    public void Parse_ReadsSubtitleStreams()
    {
        // Act
        var metadata = MediaInfoOutputParser.Parse(MultiTrackJson);

        // Assert
        metadata.ShouldNotBeNull();
        metadata.SubtitleStreams.Count.ShouldBe(1);
        metadata.SubtitleStreams[0].Codec.ShouldBe("UTF-8");
        metadata.SubtitleStreams[0].Language.ShouldBe("de");
        metadata.SubtitleStreams[0].Forced.ShouldBeTrue();
        metadata.SubtitleStreams[0].IsDefault.ShouldBeFalse();
    }

    [Test]
    public void Parse_ReadsContainerSizeAndDuration()
    {
        // Act
        var metadata = MediaInfoOutputParser.Parse(MultiTrackJson);

        // Assert
        metadata.ShouldNotBeNull();
        metadata.ContainerFormat.ShouldBe("Matroska");
        metadata.SizeBytes.ShouldBe(11811160064);
        metadata.Duration.ShouldBe(TimeSpan.FromSeconds(5918.5));
    }

    [Test]
    public void Parse_InvalidJson_ReturnsNull()
    {
        // Act / Assert
        MediaInfoOutputParser.Parse("not json").ShouldBeNull();
        MediaInfoOutputParser.Parse("").ShouldBeNull();
        MediaInfoOutputParser.Parse("{}").ShouldBeNull();
    }
}
