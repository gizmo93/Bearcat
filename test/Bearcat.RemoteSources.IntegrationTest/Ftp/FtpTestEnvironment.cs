using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;

namespace Bearcat.RemoteSources.IntegrationTest.Ftp;

[SetUpFixture]
public static class FtpTestEnvironment
{
    private static readonly Dictionary<string, IFutureDockerImage> Images = [];

    private static readonly Dictionary<FtpTestServer, FtpServer> Servers = [];

    [OneTimeTearDown]
    public static async Task OneTimeTearDown()
    {
        foreach (var server in Servers.Values)
        {
            await server.DisposeAsync();
        }

        foreach (var image in Images.Values)
        {
            await image.DisposeAsync();
        }

        Servers.Clear();
        Images.Clear();
    }

    public static async Task<FtpServer> GetServerAsync(FtpTestServer testServer)
    {
        if (!Servers.TryGetValue(testServer, out var server))
        {
            var definition = FtpServerDefinition.For(testServer);
            var image = await GetImageAsync(definition.DockerDirectory);
            server = await FtpServer.StartAsync(image, definition);
            Servers.Add(testServer, server);
        }

        return server;
    }

    private static async Task<IFutureDockerImage> GetImageAsync(string dockerDirectory)
    {
        if (!Images.TryGetValue(dockerDirectory, out var image))
        {
            image = new ImageFromDockerfileBuilder()
                .WithDockerfileDirectory(CommonDirectoryPath.GetProjectDirectory(), dockerDirectory)
                .WithDockerfile("Dockerfile")
                .WithCleanUp(true)
                .Build();
            await image.CreateAsync();
            Images.Add(dockerDirectory, image);
        }

        return image;
    }
}
