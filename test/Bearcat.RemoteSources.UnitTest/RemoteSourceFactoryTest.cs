using Bearcat.Abstractions.RemoteSource;
using Bearcat.RemoteSources.Ftp;
using Bearcat.RemoteSources.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bearcat.RemoteSources.UnitTest;

public class RemoteSourceFactoryTest
{
    private ServiceProvider serviceProvider = null!;
    private IServiceScope scope = null!;
    private IRemoteSourceFactory factory = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddRemoteSources();
        serviceProvider = services.BuildServiceProvider();
        scope = serviceProvider.CreateScope();
        factory = scope.ServiceProvider.GetRequiredService<IRemoteSourceFactory>();
    }

    [TearDown]
    public void TearDown()
    {
        scope.Dispose();
        serviceProvider.Dispose();
    }

    [Test]
    public void GetByClassName_FtpRemoteSourceClassName_ReturnsFtpRemoteSource()
    {
        // Act
        var remoteSource = factory.GetByClassName(nameof(FtpRemoteSource));

        // Assert
        remoteSource.ShouldBeOfType<FtpRemoteSource>();
    }

    [Test]
    public void GetByClassName_UnknownClassName_Throws()
    {
        // Act
        var action = () => factory.GetByClassName("UnknownRemoteSource");

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Test]
    public void GetRemoteSources_Registered_ReturnsFtpRemoteSourceWithClassNameAndFields()
    {
        // Act
        var remoteSources = factory.GetRemoteSources();

        // Assert
        var remoteSource = remoteSources.ShouldHaveSingleItem();
        remoteSource.Name.ShouldBe("FTP / FTPS");
        remoteSource.ClassName.ShouldBe(nameof(FtpRemoteSource));
        remoteSource.ConfigurationFields.ShouldBe(new FtpRemoteSource().ConfigurationFields);
    }
}
