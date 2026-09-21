using System.Net;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Refit;

namespace Bearcat.Hosters.Shared;

public static class DownloadFailure
{
    public static DownloadFileResult FromException(Exception exception)
    {
        return new DownloadFileResult(
            IsSuccess: false,
            ErrorMessages: [exception.InnerException?.Message ?? exception.Message],
            IsFileMissing: IsFileMissing(exception)
        );
    }

    private static bool IsFileMissing(Exception? exception)
    {
        return exception switch
        {
            null => false,
            HosterFileNotFoundException => true,
            HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Gone } =>
                true,
            ApiException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Gone } => true,
            _ => IsFileMissing(exception.InnerException),
        };
    }
}
