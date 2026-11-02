using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Proxies;
using Bearcat.Hosters.InversionOfControl;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest;

public class HosterFactoryTest
{
    private ServiceProvider serviceProvider = null!;
    private IServiceScope scope = null!;
    private IHosterFactory hosterFactory = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<IProxyRoutingCache>().Object);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddHosters();
        serviceProvider = services.BuildServiceProvider();
        scope = serviceProvider.CreateScope();
        hosterFactory = scope.ServiceProvider.GetRequiredService<IHosterFactory>();
    }

    [TearDown]
    public void TearDown()
    {
        scope.Dispose();
        serviceProvider.Dispose();
    }

    [Test]
    public void FileLinkHostNames_AllRegisteredHosters_NoHostNameMatchesAnotherHoster()
    {
        // Arrange
        var hosters = hosterFactory.GetHostersByName().Values.ToList();
        var hostNames = hosters
            .SelectMany(hoster =>
                hoster.FileLinkHostNames.Select(hostName => new
                {
                    HosterName = hoster.GetType().Name,
                    HostName = hostName,
                })
            )
            .ToList();

        // Act
        var conflicts = hostNames
            .SelectMany(first =>
                hostNames
                    .Where(second =>
                        second.HosterName != first.HosterName
                        && FileLinkHostMatching.HostMatchesAnyHostName(
                            first.HostName,
                            [second.HostName]
                        )
                    )
                    .Select(second =>
                        $"{first.HostName} ({first.HosterName}) matches {second.HostName} ({second.HosterName})"
                    )
            )
            .ToList();

        // Assert
        hosters.ShouldAllBe(hoster => hoster.FileLinkHostNames.Count > 0);
        conflicts.ShouldBeEmpty();
    }

    [TestCase("https://trbt.cc/zrt4rcrv6w5l.html", "Turbobit")]
    [TestCase("https://www.rapidgator.net/file/fd90f309156dca5c7b83359483451bba", "Rapidgator")]
    [TestCase("https://keep2share.cc/file/477d4eaa84d33", "Keep2Share")]
    [TestCase("https://F2S.IM/f/0beBcPPHvfen", "Fast2Share")]
    [TestCase("https://unknown-hoster.test/file/abc", null)]
    public void FindHosterByFileLink_FileLink_ReturnsHosterOwningTheHostName(
        string fileLink,
        string? expectedHosterClassName
    )
    {
        // Act
        var hoster = hosterFactory.FindHosterByFileLink(fileLink);

        // Assert
        hoster?.GetType().Name.ShouldBe(expectedHosterClassName);
        (hoster is null).ShouldBe(expectedHosterClassName is null);
    }
}
