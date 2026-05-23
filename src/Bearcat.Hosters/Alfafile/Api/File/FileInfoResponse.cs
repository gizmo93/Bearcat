namespace Bearcat.Hosters.Alfafile.Api.File;

public class FileInfoResponse
{
    public ResponseObject? Response { get; set; }

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        public UploadedFile? File { get; set; }
    }
}
