using System.Net;

namespace Bearcat.Hosters.Extensions;

public static class HttpStatusCodeExtensions
{
    extension(HttpStatusCode statusCode)
    {
        public bool IsSuccessStatusCode => (int)statusCode >= 200 && (int)statusCode <= 299;
    }
}
