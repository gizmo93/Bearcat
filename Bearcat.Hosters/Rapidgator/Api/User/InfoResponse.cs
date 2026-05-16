using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Rapidgator.Api.User;

public class InfoResponse
{
    public ResponseObject Response { get; set; } = null!;

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        public User User { get; set; } = null!;
    }

    public class User
    {
        public string Email { get; set; } = null!;

        [JsonPropertyName("is_premium")]
        public bool IsPremium { get; set; }

        [JsonPropertyName("premium_end_time")]
        public object PremiumEndTime { get; set; } = null!;

        public int State { get; set; }

        [JsonPropertyName("state_label")]
        public string StateLabel { get; set; } = null!;

        public Traffic Traffic { get; set; } = null!;

        public Storage Storage { get; set; } = null!;

        public Upload Upload { get; set; } = null!;

        [JsonPropertyName("remote_upload")]
        public RemoteUpload RemoteUpload { get; set; } = null!;
    }

    public class Traffic
    {
        public long Total { get; set; }
        public long Left { get; set; }
    }

    public class Storage
    {
        public long Total { get; set; }
        public long Left { get; set; }
    }

    public class Upload
    {
        [JsonPropertyName("max_file_size")]
        public long MaxFileSize { get; set; }

        [JsonPropertyName("nb_pipes")]
        public long NbPipes { get; set; }
    }

    public class RemoteUpload
    {
        [JsonPropertyName("max_nb_jobs")]
        public int MaxNbJobs { get; set; }

        [JsonPropertyName("refresh_time")]
        public int RefreshTime { get; set; }
    }
}
