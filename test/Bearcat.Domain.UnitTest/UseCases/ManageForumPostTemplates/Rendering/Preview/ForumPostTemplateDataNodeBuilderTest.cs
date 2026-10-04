using System.Globalization;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering.Preview;
using Scriban.Runtime;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageForumPostTemplates.Rendering.Preview;

public class ForumPostTemplateDataNodeBuilderTest
{
    [Test]
    public void Build_ImportedModel_ExposesOnlyAttributedMembersWithSnakeCaseNames()
    {
        // Arrange
        var globals = ImportModel(
            new TestRenderModel
            {
                Release = new TestReleaseModel
                {
                    ReleaseName = "Bearcat.Release-GRP",
                    HiddenValue = "secret",
                },
                Files = [],
            }
        );

        // Act
        var result = ForumPostTemplateDataNodeBuilder.Build(globals);

        // Assert
        result.ShouldBe(
            [
                new ForumPostTemplateDataNode(
                    "release",
                    null,
                    [new ForumPostTemplateDataNode("release_name", "Bearcat.Release-GRP", [])]
                ),
                new ForumPostTemplateDataNode("files", null, []),
            ],
            new DataNodeComparer()
        );
    }

    [Test]
    public void Build_ListValue_CreatesIndexedChildren()
    {
        // Arrange
        var globals = ImportModel(
            new TestRenderModel
            {
                Release = new TestReleaseModel { ReleaseName = "Name", HiddenValue = "secret" },
                Files =
                [
                    new TestFileModel { Path = "a.mkv", SizeBytes = 1 },
                    new TestFileModel { Path = "b.mkv", SizeBytes = 2 },
                ],
            }
        );

        // Act
        var result = ForumPostTemplateDataNodeBuilder.Build(globals);

        // Assert
        var files = result.Single(node => node.Name == "files");
        files.Value.ShouldBeNull();
        files.Children.ShouldBe(
            [
                new ForumPostTemplateDataNode(
                    "[0]",
                    null,
                    [
                        new ForumPostTemplateDataNode("path", "a.mkv", []),
                        new ForumPostTemplateDataNode("size_bytes", "1", []),
                    ]
                ),
                new ForumPostTemplateDataNode(
                    "[1]",
                    null,
                    [
                        new ForumPostTemplateDataNode("path", "b.mkv", []),
                        new ForumPostTemplateDataNode("size_bytes", "2", []),
                    ]
                ),
            ],
            new DataNodeComparer()
        );
    }

    [Test]
    public void Build_ScalarValues_FormatsCultureInvariantAndKeepsNull()
    {
        // Arrange
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        var globals = new ScriptObject
        {
            ["fps"] = 23.976,
            ["price"] = 1234.5m,
            ["is_default"] = true,
            ["forced"] = false,
            ["width"] = (int?)null,
            ["uploaded_at"] = new DateTime(2026, 10, 4, 13, 5, 0, DateTimeKind.Utc),
            ["urls"] = new List<string> { "https://a.example", "https://b.example" },
        };

        try
        {
            // Act
            var result = ForumPostTemplateDataNodeBuilder.Build(globals);

            // Assert
            result.ShouldBe(
                [
                    new ForumPostTemplateDataNode("fps", "23.976", []),
                    new ForumPostTemplateDataNode("price", "1234.5", []),
                    new ForumPostTemplateDataNode("is_default", "true", []),
                    new ForumPostTemplateDataNode("forced", "false", []),
                    new ForumPostTemplateDataNode("width", null, []),
                    new ForumPostTemplateDataNode("uploaded_at", "10/04/2026 13:05:00", []),
                    new ForumPostTemplateDataNode(
                        "urls",
                        null,
                        [
                            new ForumPostTemplateDataNode("[0]", "https://a.example", []),
                            new ForumPostTemplateDataNode("[1]", "https://b.example", []),
                        ]
                    ),
                ],
                new DataNodeComparer()
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public void Build_NestedScriptObject_CreatesChildrenFromKeys()
    {
        // Arrange
        var configLinks = new ScriptObject
        {
            ["full"] = "https://img.example/full.jpg",
            ["thumbnail"] = "https://img.example/thumb.jpg",
        };
        var globals = new ScriptObject
        {
            ["imagelinks"] = new ScriptObject { ["imgbb_cover"] = configLinks },
        };

        // Act
        var result = ForumPostTemplateDataNodeBuilder.Build(globals);

        // Assert
        result.ShouldBe(
            [
                new ForumPostTemplateDataNode(
                    "imagelinks",
                    null,
                    [
                        new ForumPostTemplateDataNode(
                            "imgbb_cover",
                            null,
                            [
                                new ForumPostTemplateDataNode(
                                    "full",
                                    "https://img.example/full.jpg",
                                    []
                                ),
                                new ForumPostTemplateDataNode(
                                    "thumbnail",
                                    "https://img.example/thumb.jpg",
                                    []
                                ),
                            ]
                        ),
                    ]
                ),
            ],
            new DataNodeComparer()
        );
    }

    private static ScriptObject ImportModel(TestRenderModel model)
    {
        var globals = new ScriptObject();
        globals.Import(model, ForumPostTemplateVariableCatalog.ShouldExposeMember);
        return globals;
    }

    private sealed class DataNodeComparer : IEqualityComparer<ForumPostTemplateDataNode>
    {
        public bool Equals(ForumPostTemplateDataNode? x, ForumPostTemplateDataNode? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            return x.Name == y.Name
                && x.Value == y.Value
                && x.Children.SequenceEqual(y.Children, this);
        }

        public int GetHashCode(ForumPostTemplateDataNode obj)
        {
            return HashCode.Combine(obj.Name, obj.Value, obj.Children.Count);
        }
    }

    private sealed record TestRenderModel
    {
        [ForumPostTemplateVariable("Release.", IncludeChildren = true)]
        public required TestReleaseModel Release { get; init; }

        [ForumPostTemplateVariable("Files.", LoopVariable = "file")]
        public required IReadOnlyList<TestFileModel> Files { get; init; }
    }

    private sealed record TestReleaseModel
    {
        [ForumPostTemplateVariable("Release name.")]
        public required string ReleaseName { get; init; }

        public required string HiddenValue { get; init; }
    }

    private sealed record TestFileModel
    {
        [ForumPostTemplateVariable("Path.")]
        public required string Path { get; init; }

        [ForumPostTemplateVariable("Size.")]
        public required long SizeBytes { get; init; }
    }
}
