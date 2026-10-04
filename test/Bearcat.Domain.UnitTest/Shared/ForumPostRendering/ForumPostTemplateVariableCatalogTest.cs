using Bearcat.Domain.Shared.ForumPostRendering;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ForumPostRendering;

public class ForumPostTemplateVariableCatalogTest
{
    [Test]
    public void GetVariables_RootType_ReturnsTopLevelNodesInDeclarationOrder()
    {
        // Act
        var result = ForumPostTemplateVariableCatalog.GetVariables(typeof(TestRenderModel));

        // Assert
        result.Select(node => node.Name).ShouldBe(["release", "uploads", "tags", "files"]);
    }

    [Test]
    public void GetVariables_ValueProperty_CreatesInsertableLeafWithFullPath()
    {
        // Act
        var result = ForumPostTemplateVariableCatalog.GetVariables(typeof(TestRenderModel));

        // Assert
        var releaseName = GetNode(result, "release").Children[0];
        releaseName.Name.ShouldBe("release_name");
        releaseName.Path.ShouldBe("release.release_name");
        releaseName.Description.ShouldBe("Release name.");
        releaseName.LoopStatement.ShouldBeNull();
        releaseName.Children.ShouldBeEmpty();
        releaseName.Insertion.ShouldBe(
            new ForumPostTemplateVariableInsertion(
                "{{ release.release_name }}",
                "{{ release.release_name }}".Length
            )
        );
    }

    [Test]
    public void GetVariables_ObjectWithIncludeChildren_CreatesNonInsertableNodeWithMembers()
    {
        // Act
        var result = ForumPostTemplateVariableCatalog.GetVariables(typeof(TestRenderModel));

        // Assert
        var release = GetNode(result, "release");
        release.Path.ShouldBe("release");
        release.Description.ShouldBe("Release.");
        release.Insertion.ShouldBeNull();
        release.LoopStatement.ShouldBeNull();
        release.Children.Select(node => node.Name).ShouldBe(["release_name", "main_file"]);
        var mainFile = GetNode(release.Children, "main_file");
        mainFile.Insertion.ShouldBeNull();
        mainFile.Children.Select(node => node.Path).ShouldBe(["release.main_file.path"]);
    }

    [Test]
    public void GetVariables_LoopProperty_CreatesLoopSnippetWithCursorOnEmptyMiddleLine()
    {
        // Act
        var result = ForumPostTemplateVariableCatalog.GetVariables(typeof(TestRenderModel));

        // Assert
        var uploads = GetNode(result, "uploads");
        uploads.Path.ShouldBe("uploads");
        uploads.LoopStatement.ShouldBe("for upload in uploads");
        uploads.Insertion.ShouldNotBeNull();
        uploads.Insertion.Text.ShouldBe("{{ for upload in uploads }}\n\n{{ end }}");
        uploads
            .Insertion.Text[..uploads.Insertion.CursorOffset]
            .ShouldBe("{{ for upload in uploads }}\n");
    }

    [Test]
    public void GetVariables_LoopProperty_PrefixesElementMembersWithLoopVariable()
    {
        // Act
        var result = ForumPostTemplateVariableCatalog.GetVariables(typeof(TestRenderModel));

        // Assert
        var uploads = GetNode(result, "uploads");
        uploads
            .Children.Select(node => node.Path)
            .ShouldBe(["upload.name", "upload.link_crypters"]);
        GetNode(uploads.Children, "name")
            .Insertion.ShouldNotBeNull()
            .Text.ShouldBe("{{ upload.name }}");
    }

    [Test]
    public void GetVariables_NestedLoop_UsesOuterLoopVariableInLoopStatementAndInnerLoopVariableForMembers()
    {
        // Act
        var result = ForumPostTemplateVariableCatalog.GetVariables(typeof(TestRenderModel));

        // Assert
        var linkCrypters = GetNode(GetNode(result, "uploads").Children, "link_crypters");
        linkCrypters.LoopStatement.ShouldBe("for crypter in upload.link_crypters");
        linkCrypters
            .Insertion.ShouldNotBeNull()
            .Text.ShouldBe("{{ for crypter in upload.link_crypters }}\n\n{{ end }}");
        linkCrypters.Children.Select(node => node.Path).ShouldBe(["crypter.container_link"]);
    }

    [Test]
    public void GetVariables_LoopWithElementTypeOverride_UsesElementTypeMembers()
    {
        // Act
        var result = ForumPostTemplateVariableCatalog.GetVariables(typeof(TestRenderModel));

        // Assert
        var files = GetNode(result, "files");
        files.LoopStatement.ShouldBe("for file in files");
        files.Children.Select(node => node.Path).ShouldBe(["file.path"]);
    }

    [Test]
    public void GetVariables_LoopOverSimpleValues_CreatesLoopWithoutChildren()
    {
        // Act
        var result = ForumPostTemplateVariableCatalog.GetVariables(typeof(TestRenderModel));

        // Assert
        var tags = GetNode(result, "tags");
        tags.LoopStatement.ShouldBe("for tag in tags");
        tags.Children.ShouldBeEmpty();
    }

    private static ForumPostTemplateVariableNode GetNode(
        IReadOnlyList<ForumPostTemplateVariableNode> nodes,
        string name
    )
    {
        return nodes.Single(node => node.Name == name);
    }

    private sealed record TestRenderModel
    {
        [ForumPostTemplateVariable("Release.", IncludeChildren = true)]
        public required TestReleaseModel Release { get; init; }

        [ForumPostTemplateVariable("Uploads.", LoopVariable = "upload")]
        public required IReadOnlyList<TestUploadModel> Uploads { get; init; }

        [ForumPostTemplateVariable("Tags.", LoopVariable = "tag")]
        public required IReadOnlyList<string> Tags { get; init; }

        [ForumPostTemplateVariable(
            "Files.",
            LoopVariable = "file",
            ElementType = typeof(TestFileModel)
        )]
        public required object Files { get; init; }
    }

    private sealed record TestReleaseModel
    {
        [ForumPostTemplateVariable("Release name.")]
        public required string ReleaseName { get; init; }

        [ForumPostTemplateVariable("Main file.", IncludeChildren = true)]
        public required TestFileModel MainFile { get; init; }
    }

    private sealed record TestUploadModel
    {
        [ForumPostTemplateVariable("Upload name.")]
        public required string Name { get; init; }

        [ForumPostTemplateVariable("Link crypters.", LoopVariable = "crypter")]
        public required IReadOnlyList<TestLinkCrypterModel> LinkCrypters { get; init; }
    }

    private sealed record TestLinkCrypterModel
    {
        [ForumPostTemplateVariable("Container link.")]
        public required string ContainerLink { get; init; }
    }

    private sealed record TestFileModel
    {
        [ForumPostTemplateVariable("Path.")]
        public required string Path { get; init; }
    }
}
