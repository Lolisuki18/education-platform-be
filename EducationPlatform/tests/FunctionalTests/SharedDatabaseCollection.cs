using Xunit;

namespace FunctionalTests
{
    [CollectionDefinition("Shared database collection")]
    public class SharedDatabaseCollection : ICollectionFixture<IntegrationTests.CustomWebApplicationFactory>
    {
    }
}
