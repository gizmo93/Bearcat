using Bearcat.Hosters.Shared;

namespace Bearcat.Hosters.Alfafile.Api.File;

public class FileInfoResponse : IResponseWithStatus
{
    public ResponseObject? Response { get; set; }

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        public UploadedFile? File { get; set; }
    }
}
