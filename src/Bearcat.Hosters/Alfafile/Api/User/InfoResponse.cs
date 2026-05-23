namespace Bearcat.Hosters.Alfafile.Api.User;

public class InfoResponse
{
    public ResponseObject? Response { get; set; }

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        public LoginResponse.User User { get; set; } = null!;
    }
}
