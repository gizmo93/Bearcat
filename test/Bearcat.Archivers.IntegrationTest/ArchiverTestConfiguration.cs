using Microsoft.Extensions.Configuration;

namespace Bearcat.Archivers.IntegrationTest;

public static class ArchiverTestConfiguration
{
    public static IConfiguration Create()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Archivers:RarPath"] = "rar",
                    ["Archivers:SevenZipPath"] = "7z",
                }
            )
            .Build();
    }
}
