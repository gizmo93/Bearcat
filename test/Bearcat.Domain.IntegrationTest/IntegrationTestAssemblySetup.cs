using Bearcat.IntegrationTest.Utils;

[assembly: Parallelizable(ParallelScope.All)]
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]

namespace Bearcat.Domain.IntegrationTest;

[SetUpFixture]
public class IntegrationTestAssemblySetup
{
    [OneTimeTearDown]
    public async Task DisposeSharedDatabaseResourcesAsync()
    {
        await BearcatIntegrationTestDatabase.DisposeSharedResourcesAsync();
    }
}
