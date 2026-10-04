using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Website.Pages.ManageForumPostTemplates.Editor;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageForumPostTemplates.Editor;

public class ForumPostTemplateVariableTreeFilterTest
{
    private static readonly IReadOnlyList<ForumPostTemplateVariableNode> Variables =
    [
        ForumPostTemplateVariableNode.CreateObjectNode(
            "release",
            "release",
            "Release data.",
            [
                ForumPostTemplateVariableNode.CreateValueNode(
                    "name",
                    "release.name",
                    "Release name."
                ),
                ForumPostTemplateVariableNode.CreateValueNode(
                    "nfo",
                    "release.nfo",
                    "NFO file content."
                ),
            ]
        ),
        ForumPostTemplateVariableNode.CreateLoopNode(
            "uploads",
            "uploads",
            "Loop over upload configurations.",
            "upload",
            [
                ForumPostTemplateVariableNode.CreateValueNode(
                    "name",
                    "upload.name",
                    "Upload configuration name."
                ),
                ForumPostTemplateVariableNode.CreateLoopNode(
                    "link_crypters",
                    "upload.link_crypters",
                    "Loop over link crypter container links.",
                    "crypter",
                    [
                        ForumPostTemplateVariableNode.CreateValueNode(
                            "container_link",
                            "crypter.container_link",
                            "Container link."
                        ),
                    ]
                ),
            ]
        ),
    ];

    [Test]
    public void CreateKey_WithParentKey_JoinsKeysWithSlash()
    {
        // Act
        var result = ForumPostTemplateVariableTreeFilter.CreateKey("uploads", "name");

        // Assert
        result.ShouldBe("uploads/name");
    }

    [Test]
    public void Filter_EmptySearchTerm_ReturnsAllNodesWithoutExpandedKeys()
    {
        // Act
        var result = ForumPostTemplateVariableTreeFilter.Filter(Variables, "  ");

        // Assert
        result.Nodes.ShouldBeSameAs(Variables);
        result.ExpandedKeys.ShouldBeEmpty();
    }

    [Test]
    public void Filter_MatchingLeaf_KeepsAncestorsWithOnlyMatchingChildrenAndExpandsThem()
    {
        // Act
        var result = ForumPostTemplateVariableTreeFilter.Filter(Variables, "CONTAINER");

        // Assert
        var uploads = result.Nodes.ShouldHaveSingleItem();
        uploads.Name.ShouldBe("uploads");
        var linkCrypters = uploads.Children.ShouldHaveSingleItem();
        linkCrypters.Name.ShouldBe("link_crypters");
        linkCrypters.Children.ShouldHaveSingleItem().Name.ShouldBe("container_link");
        result.ExpandedKeys.ShouldBe(["uploads/link_crypters", "uploads"], ignoreOrder: true);
    }

    [Test]
    public void Filter_SearchTermMatchingPath_ReturnsNodesWhosePathContainsTerm()
    {
        // Act
        var result = ForumPostTemplateVariableTreeFilter.Filter(Variables, "upload.name");

        // Assert
        var uploads = result.Nodes.ShouldHaveSingleItem();
        uploads.Children.ShouldHaveSingleItem().Path.ShouldBe("upload.name");
        result.ExpandedKeys.ShouldBe(["uploads"]);
    }

    [Test]
    public void Filter_SearchTermMatchingDescription_ReturnsMatchingNode()
    {
        // Act
        var result = ForumPostTemplateVariableTreeFilter.Filter(Variables, "nfo file");

        // Assert
        var release = result.Nodes.ShouldHaveSingleItem();
        release.Children.ShouldHaveSingleItem().Name.ShouldBe("nfo");
        result.ExpandedKeys.ShouldBe(["release"]);
    }

    [Test]
    public void Filter_MatchingParent_KeepsAllChildrenAndExpandsOnlyAncestorsOfMatches()
    {
        // Act
        var result = ForumPostTemplateVariableTreeFilter.Filter(Variables, "link_crypters");

        // Assert
        var linkCrypters = result.Nodes.ShouldHaveSingleItem().Children.ShouldHaveSingleItem();
        linkCrypters.ShouldBeSameAs(Variables[1].Children[1]);
        result.ExpandedKeys.ShouldBe(["uploads"]);
    }

    [Test]
    public void Filter_NoMatch_ReturnsNoNodes()
    {
        // Act
        var result = ForumPostTemplateVariableTreeFilter.Filter(Variables, "missing");

        // Assert
        result.Nodes.ShouldBeEmpty();
        result.ExpandedKeys.ShouldBeEmpty();
    }
}
