using System.Xml.Linq;
using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Bearcat.UploadCompletionActions.IntelligenXmlExport;
using Shouldly;

namespace Bearcat.UploadCompletionActions.UnitTest.IntelligenXmlExport;

public class IntelligenXmlDocumentBuilderTest
{
    private const long Mebibyte = 1048576;

    [Test]
    public void Build_FullMovieInput_WritesTemplateTypeControlsAndMirrors()
    {
        // Arrange
        var input = new UploadCompletionActionInput(
            Release: CreateRelease(contentType: "Movie"),
            CollectionReleaseNames: [],
            IsReupload: false,
            Mirrors:
            [
                new UploadCompletionActionMirrorDto(
                    HosterClassName: "RapidGator",
                    HosterName: "rapidgator.net",
                    Links: ["https://rapidgator.net/file/1", "https://rapidgator.net/file/2"],
                    TotalSizeBytes: 4321 * Mebibyte + 100,
                    LargestPartSizeBytes: 500 * Mebibyte,
                    PartCount: 2,
                    ArchivePassword: "secret",
                    LinkCrypterContainers:
                    [
                        new UploadCompletionActionLinkCrypterContainerDto(
                            LinkCrypterClassName: "FileCrypt",
                            LinkCrypterName: "filecrypt.cc",
                            Url: "https://filecrypt.cc/Container/ABC.html"
                        ),
                        new UploadCompletionActionLinkCrypterContainerDto(
                            LinkCrypterClassName: "KeepLinks",
                            LinkCrypterName: "keeplinks.org",
                            Url: "https://keeplinks.org/p/abc"
                        ),
                    ]
                ),
                new UploadCompletionActionMirrorDto(
                    HosterClassName: "DdownloadCom",
                    HosterName: "ddownload.com",
                    Links: ["https://ddownload.com/1"],
                    TotalSizeBytes: null,
                    LargestPartSizeBytes: null,
                    PartCount: 1,
                    ArchivePassword: "secret",
                    LinkCrypterContainers: []
                ),
            ],
            AllHostersLinkCrypterContainers:
            [
                new UploadCompletionActionLinkCrypterContainerDto(
                    LinkCrypterClassName: "ToLinkTo",
                    LinkCrypterName: "tolink.to",
                    Url: "https://tolink.to/f/all"
                ),
            ],
            Images:
            [
                new UploadCompletionActionImageDto(
                    ImageHosterClassName: "DirectUpload",
                    ImageHosterName: "DirectUpload",
                    Url: "https://directupload.eu/cover.jpg",
                    IsCover: true
                ),
                new UploadCompletionActionImageDto(
                    ImageHosterClassName: "DirectUpload",
                    ImageHosterName: "DirectUpload",
                    Url: "https://directupload.eu/screenshot.jpg",
                    IsCover: false
                ),
            ]
        );

        // Act
        var document = IntelligenXmlDocumentBuilder.Build(input);

        // Assert
        var root = document.Root.ShouldNotBeNull();
        root.Name.LocalName.ShouldBe("xml");

        var templateType = root.Element("templatetype").ShouldNotBeNull();
        templateType.Value.ShouldBe("Movie");
        templateType.Attribute("filename")!.Value.ShouldBe("Movie");

        var controls = root.Element("controls").ShouldNotBeNull();

        controls
            .Elements()
            .Select(control => control.Name.LocalName)
            .ShouldBe([
                "IReleaseName",
                "ITitle",
                "IPicture",
                "IRuntime",
                "IGenre",
                "ILanguage",
                "IAudioStream",
                "IVideoCodec",
                "IVideoStream",
                "IPassword",
                "INFO",
                "IDescription",
            ]);

        var releaseName = controls.Element("IReleaseName").ShouldNotBeNull();
        releaseName.Element("title")!.Value.ShouldBe("#");
        releaseName
            .Element("value")!
            .Value.ShouldBe("Bearcat.Movie.2026.German.1080p.WEB.x264-GRP");
        releaseName.Element("value")!.Attribute("list")!.Value.ShouldBe("#");
        releaseName.Element("position")!.Attribute("width")!.Value.ShouldBe("300");
        releaseName.Element("position")!.Attribute("height")!.Value.ShouldBe(string.Empty);

        controls.Element("IRuntime")!.Element("value")!.Value.ShouldBe("118");
        controls.Element("IGenre")!.Element("value")!.Value.ShouldBe("Action");
        controls.Element("ILanguage")!.Element("value")!.Value.ShouldBe("GER;eng");
        controls.Element("IAudioStream")!.Element("value")!.Value.ShouldBe("Dolby Digital 5.1");
        controls.Element("IVideoCodec")!.Element("value")!.Value.ShouldBe("MPEG-4 H.264 / AVC");
        controls.Element("IVideoStream")!.Element("value")!.Value.ShouldBe("WEB-DL");
        controls.Element("IPassword")!.Element("value")!.Value.ShouldBe("secret");
        controls.Element("INFO")!.Element("value")!.Value.ShouldBe("NFO line one  \r\nline two");

        var picture = controls.Element("IPicture").ShouldNotBeNull();
        picture.Element("value")!.Value.ShouldBe("https://directupload.eu/cover.jpg");

        var pictureHoster = picture.Element("hosters")!.Elements("hoster").ShouldHaveSingleItem();
        pictureHoster.Attribute("name")!.Value.ShouldBe("Directupload.net");
        pictureHoster.Value.ShouldBe("https://directupload.eu/cover.jpg");

        var mirrors = root.Element("mirrors")!.Elements("mirror").ToList();
        mirrors.Count.ShouldBe(3);

        var firstDirectlink = mirrors[0].Element("directlink").ShouldNotBeNull();
        firstDirectlink.Attribute("size")!.Value.ShouldBe("4321");
        firstDirectlink.Attribute("partsize")!.Value.ShouldBe("500");
        firstDirectlink.Attribute("parts")!.Value.ShouldBe("2");
        firstDirectlink.Attribute("hoster")!.Value.ShouldBe("rapidgator.net");
        firstDirectlink.Value.ShouldBe(
            "https://rapidgator.net/file/1\r\nhttps://rapidgator.net/file/2"
        );

        var firstMirrorCrypters = mirrors[0].Elements("crypter").ToList();
        firstMirrorCrypters.Count.ShouldBe(2);
        firstMirrorCrypters[0].Attribute("name")!.Value.ShouldBe("filecrypt.cc");
        firstMirrorCrypters[0].Attribute("size")!.Value.ShouldBe("4321");
        firstMirrorCrypters[0].Value.ShouldBe("https://filecrypt.cc/Container/ABC.html");
        firstMirrorCrypters[1].Attribute("name")!.Value.ShouldBe("keeplinks.org");
        firstMirrorCrypters[1].Value.ShouldBe("https://keeplinks.org/p/abc");

        var secondDirectlink = mirrors[1].Element("directlink").ShouldNotBeNull();
        secondDirectlink.Attribute("size")!.Value.ShouldBe("4321");
        secondDirectlink.Attribute("partsize")!.Value.ShouldBe("500");

        mirrors[2].Element("directlink").ShouldBeNull();
        var allHostersCrypter = mirrors[2].Elements("crypter").ShouldHaveSingleItem();
        allHostersCrypter.Attribute("name")!.Value.ShouldBe("tolink.to");
        allHostersCrypter.Value.ShouldBe("https://tolink.to/f/all");
    }

    [Test]
    public void Build_SizesUnknownOnAllMirrorsAndGameOnConsole_OmitsSizeAttributesAndUsesOtherTemplate()
    {
        // Arrange
        var input = new UploadCompletionActionInput(
            Release: CreateRelease(contentType: "Game") with
            {
                Platform = "NintendoSwitch",
            },
            CollectionReleaseNames: [],
            IsReupload: true,
            Mirrors:
            [
                new UploadCompletionActionMirrorDto(
                    HosterClassName: "RapidGator",
                    HosterName: "rapidgator.net",
                    Links: ["https://rapidgator.net/file/1"],
                    TotalSizeBytes: null,
                    LargestPartSizeBytes: null,
                    PartCount: 1,
                    ArchivePassword: null,
                    LinkCrypterContainers: []
                ),
            ],
            AllHostersLinkCrypterContainers: [],
            Images: []
        );

        // Act
        var document = IntelligenXmlDocumentBuilder.Build(input);

        // Assert
        document.Root!.Element("templatetype")!.Value.ShouldBe("Other");

        var directlink = document
            .Root.Element("mirrors")!
            .Elements("mirror")
            .ShouldHaveSingleItem()
            .Element("directlink")
            .ShouldNotBeNull();

        directlink.Attribute("size").ShouldBeNull();
        directlink.Attribute("partsize").ShouldBeNull();
        directlink.Attribute("parts")!.Value.ShouldBe("1");
    }

    private static UploadCompletionActionReleaseDto CreateRelease(string contentType)
    {
        return new UploadCompletionActionReleaseDto(
            Name: "Bearcat.Movie.2026.German.1080p.WEB.x264-GRP",
            ContentType: contentType,
            Platform: null,
            Title: "Bearcat Movie",
            Genre: "Action, Thriller",
            Description: "A bear and a cat.",
            RuntimeMinutes: 118,
            VideoType: "WEB-DL",
            VideoCodec: "h264",
            AudioType: null,
            AudioCodec: "eac3",
            AudioChannels: 6,
            AudioLanguages: ["deu", "ger", "eng"],
            Nfo: "NFO line one\t\u0001\r\nline two"
        );
    }
}
