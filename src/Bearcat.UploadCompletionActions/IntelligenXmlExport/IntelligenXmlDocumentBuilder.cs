using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Bearcat.Abstractions.UploadCompletionAction.Dto;

namespace Bearcat.UploadCompletionActions.IntelligenXmlExport;

public static class IntelligenXmlDocumentBuilder
{
    private const double BytesPerMebibyte = 1048576;

    private const string KeepIntelligenDefault = "#";

    private const string WideControlWidth = "300";

    private const string NarrowControlWidth = "147";

    private static readonly Dictionary<string, string> IntelligenVideoCodecNamesByCodec = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        { "mpeg1video", "MPEG-1" },
        { "mpeg2video", "MPEG-2" },
        { "mpeg4", "MPEG-4 DivX / Xvid" },
        { "h264", "MPEG-4 H.264 / AVC" },
        { "hevc", "MPEG-H H.265 / HEVC" },
        { "vc1", "VC-1" },
    };

    private static readonly Dictionary<string, string> IntelligenLanguageNamesByLanguageCode = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        { "deu", "GER" },
    };

    private static readonly Dictionary<string, string> IntelligenImageHosterNamesByClassName = new()
    {
        { "DirectUpload", "Directupload.net" },
    };

    public static XDocument Build(UploadCompletionActionInput input)
    {
        var templateTypeName = GetTemplateTypeName(input.Release);

        var rootElement = new XElement(
            "xml",
            new XElement(
                "templatetype",
                new XAttribute("filename", templateTypeName),
                templateTypeName
            ),
            new XElement("controls", BuildControlElements(input)),
            new XElement("mirrors", BuildMirrorElements(input))
        );

        return new XDocument(new XDeclaration("1.0", "utf-8", null), rootElement);
    }

    public static byte[] ToUtf8Bytes(XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Entitize,
        };

        using var stream = new MemoryStream();

        using (var writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }

    private static string? GetAudioStreamNameOrNullWhenUnknown(
        UploadCompletionActionReleaseDto release
    )
    {
        var audioCodec = release.AudioCodec?.ToLowerInvariant();

        return (audioCodec, release.AudioChannels) switch
        {
            ("ac3" or "eac3", 2) => "Dolby Digital 2.0",
            ("ac3" or "eac3", 3) => "Dolby Digital 2.1",
            ("ac3" or "eac3", 6) => "Dolby Digital 5.1",
            ("ac3" or "eac3", 8) => "Dolby Digital 7.1",
            ("dts", 6) => "DTS 5.1",
            (_, 1) => "Mono",
            (_, 2) => "Stereo",
            _ => null,
        };
    }

    private static string GetTemplateTypeName(UploadCompletionActionReleaseDto release)
    {
        return release.ContentType switch
        {
            "Movie" or "TvShowEpisode" => "Movie",
            "Game" when release.Platform is null or "Windows" => "PCGames",
            _ => "Other",
        };
    }

    private static List<XElement> BuildControlElements(UploadCompletionActionInput input)
    {
        var release = input.Release;

        var archivePassword = input
            .Mirrors.Select(mirror => mirror.ArchivePassword)
            .FirstOrDefault(password => !string.IsNullOrEmpty(password));

        var languageNames = release
            .AudioLanguages.Select(languageCode =>
                IntelligenLanguageNamesByLanguageCode.GetValueOrDefault(languageCode, languageCode)
            )
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var languages = languageNames.Count == 0 ? null : string.Join(';', languageNames);

        var firstGenre = release.Genre?.Split(',').First().Trim();

        var videoCodec = release.VideoCodec is null
            ? null
            : IntelligenVideoCodecNamesByCodec.GetValueOrDefault(
                release.VideoCodec,
                release.VideoCodec
            );

        var controlElements = new List<XElement>();

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "IReleaseName",
            value: release.Name,
            width: WideControlWidth
        );

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "ITitle",
            value: release.Title,
            width: NarrowControlWidth
        );

        AddPictureControlElementWhenCoverIsKnown(controlElements, input.Images);

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "IRuntime",
            value: release.RuntimeMinutes?.ToString(CultureInfo.InvariantCulture),
            width: NarrowControlWidth
        );

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "IGenre",
            value: firstGenre,
            width: NarrowControlWidth
        );

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "ILanguage",
            value: languages,
            width: NarrowControlWidth
        );

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "IAudioStream",
            value: release.AudioType ?? GetAudioStreamNameOrNullWhenUnknown(release),
            width: NarrowControlWidth
        );

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "IVideoCodec",
            value: videoCodec,
            width: NarrowControlWidth
        );

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "IVideoStream",
            value: release.VideoType,
            width: NarrowControlWidth
        );

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "IPassword",
            value: archivePassword,
            width: NarrowControlWidth
        );

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "INFO",
            value: release.Nfo,
            width: WideControlWidth
        );

        AddControlElementWhenValueIsKnown(
            controlElements: controlElements,
            controlName: "IDescription",
            value: release.Description,
            width: WideControlWidth
        );

        return controlElements;
    }

    private static void AddControlElementWhenValueIsKnown(
        List<XElement> controlElements,
        string controlName,
        string? value,
        string width
    )
    {
        if (value is null)
        {
            return;
        }

        controlElements.Add(
            BuildControlElement(
                controlName: controlName,
                value: value,
                width: width,
                hostersElement: null
            )
        );
    }

    private static void AddPictureControlElementWhenCoverIsKnown(
        List<XElement> controlElements,
        IReadOnlyList<UploadCompletionActionImageDto> images
    )
    {
        var coverImages = images.Where(image => image.IsCover).ToList();

        if (coverImages.Count == 0)
        {
            return;
        }

        var hosterElements = coverImages
            .Where(image =>
                IntelligenImageHosterNamesByClassName.ContainsKey(image.ImageHosterClassName)
            )
            .Select(image => new XElement(
                "hoster",
                new XAttribute(
                    "name",
                    IntelligenImageHosterNamesByClassName[image.ImageHosterClassName]
                ),
                image.Url
            ))
            .ToList();

        controlElements.Add(
            BuildControlElement(
                controlName: "IPicture",
                value: coverImages.First().Url,
                width: WideControlWidth,
                hostersElement: new XElement("hosters", hosterElements)
            )
        );
    }

    private static XElement BuildControlElement(
        string controlName,
        string value,
        string width,
        XElement? hostersElement
    )
    {
        return new XElement(
            controlName,
            new XElement(
                "title",
                new XAttribute("hint", KeepIntelligenDefault),
                KeepIntelligenDefault
            ),
            new XElement(
                "value",
                new XAttribute("list", KeepIntelligenDefault),
                ReplaceControlCharactersWithSpaces(value)
            ),
            hostersElement,
            new XElement(
                "position",
                new XAttribute("left", string.Empty),
                new XAttribute("top", string.Empty),
                new XAttribute("width", width),
                new XAttribute("height", string.Empty)
            )
        );
    }

    private static string ReplaceControlCharactersWithSpaces(string value)
    {
        var characters = value
            .Select(character =>
                character < ' ' && character != '\r' && character != '\n' ? ' ' : character
            )
            .ToArray();

        return new string(characters);
    }

    private static List<XElement> BuildMirrorElements(UploadCompletionActionInput input)
    {
        var knownTotalSizeBytes = input
            .Mirrors.Select(mirror => mirror.TotalSizeBytes)
            .FirstOrDefault(sizeBytes => sizeBytes is not null);

        var knownLargestPartSizeBytes = input
            .Mirrors.Select(mirror => mirror.LargestPartSizeBytes)
            .FirstOrDefault(sizeBytes => sizeBytes is not null);

        var mirrorElements = new List<XElement>();

        foreach (var mirror in input.Mirrors)
        {
            var sizeAttributes = BuildSizeAttributes(
                totalSizeBytes: mirror.TotalSizeBytes ?? knownTotalSizeBytes,
                largestPartSizeBytes: mirror.LargestPartSizeBytes ?? knownLargestPartSizeBytes
            );

            var directlinkElement = new XElement(
                "directlink",
                sizeAttributes,
                new XAttribute("hoster", mirror.HosterName),
                new XAttribute("parts", mirror.Links.Count),
                string.Join("\r\n", mirror.Links)
            );

            var crypterElements = BuildCrypterElements(
                mirror.LinkCrypterContainers,
                sizeAttributes
            );

            mirrorElements.Add(new XElement("mirror", directlinkElement, crypterElements));
        }

        var allHostersCrypterElements = BuildCrypterElements(
            input.AllHostersLinkCrypterContainers,
            BuildSizeAttributes(knownTotalSizeBytes, knownLargestPartSizeBytes)
        );

        if (allHostersCrypterElements.Count > 0)
        {
            mirrorElements.Add(new XElement("mirror", allHostersCrypterElements));
        }

        return mirrorElements;
    }

    private static List<XElement> BuildCrypterElements(
        IReadOnlyList<UploadCompletionActionLinkCrypterContainerDto> linkCrypterContainers,
        List<XAttribute> sizeAttributes
    )
    {
        return linkCrypterContainers
            .Select(container => new XElement(
                "crypter",
                new XAttribute("name", container.LinkCrypterName),
                sizeAttributes,
                container.Url
            ))
            .ToList();
    }

    private static List<XAttribute> BuildSizeAttributes(
        long? totalSizeBytes,
        long? largestPartSizeBytes
    )
    {
        var sizeAttributes = new List<XAttribute>();

        if (totalSizeBytes is not null)
        {
            sizeAttributes.Add(new XAttribute("size", ToRoundedMebibytes(totalSizeBytes.Value)));
        }

        if (largestPartSizeBytes is not null)
        {
            sizeAttributes.Add(
                new XAttribute("partsize", ToRoundedMebibytes(largestPartSizeBytes.Value))
            );
        }

        return sizeAttributes;
    }

    private static long ToRoundedMebibytes(long sizeBytes)
    {
        return (long)Math.Round(sizeBytes / BytesPerMebibyte);
    }
}
