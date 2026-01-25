using System.Net;

namespace BearCat.Core.Infrastructure.Hosters.Extensions;

public static class HttpStatusCodeExtensions
{
    extension(HttpStatusCode statusCode)
    {
        public bool IsSuccessStatusCode => (int)statusCode >= 200 && (int)statusCode <= 299;
    }
}
