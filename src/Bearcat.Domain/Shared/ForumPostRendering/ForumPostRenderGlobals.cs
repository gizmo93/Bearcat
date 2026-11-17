using Scriban.Runtime;

namespace Bearcat.Domain.Shared.ForumPostRendering;

public record ForumPostRenderGlobals(ScriptObject Globals, IReadOnlyList<string> BlockedReasons);
