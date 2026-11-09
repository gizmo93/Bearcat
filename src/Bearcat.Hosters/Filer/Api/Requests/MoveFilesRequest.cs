using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Requests;

public record MoveFilesRequest(
    [property: JsonPropertyName("files")] IReadOnlyList<string> FileHashes,
    [property: JsonPropertyName("targetFolder")] string TargetFolderHash,
    [property: JsonPropertyName("editCodes")]
        IReadOnlyDictionary<string, string> EditCodePerFileHash
);
